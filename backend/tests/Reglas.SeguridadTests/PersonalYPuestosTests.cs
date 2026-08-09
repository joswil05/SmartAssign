using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E3.1 y UT-E3.4 (docs/PROGRESO.md), contra una base descartable —
/// mismo patrón que EsquemaBaseTests.
/// </summary>
public class PersonalYPuestosTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    public async Task InitializeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await using var ctx = CrearContexto();
        await ctx.Database.EnsureDeletedAsync();
    }

    // ═══ E3.1 — CK_Puesto_umbrales (00 §A4, §A14) ═══

    [Fact]
    public async Task CK_Puesto_umbrales_rechaza_critico_igual_al_sugerido()
    {
        await using var ctx = CrearContexto();
        ctx.Puestos.Add(new Puesto
        {
            LineaId = 1, Codigo = "U1", NombrePuesto = "Prueba", Tipo = "rotativo",
            HorasEnPuesto = 5, UmbralCriticoHoras = 5, // igual, no mayor -> debe rechazar
        });

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CK_Puesto_umbrales_rechaza_critico_menor_al_sugerido()
    {
        await using var ctx = CrearContexto();
        ctx.Puestos.Add(new Puesto
        {
            LineaId = 1, Codigo = "U2", NombrePuesto = "Prueba", Tipo = "rotativo",
            HorasEnPuesto = 5, UmbralCriticoHoras = 3,
        });

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task CK_Puesto_umbrales_acepta_critico_mayor_al_sugerido()
    {
        // Nota: Puesto tiene RLS (04 §6.3, etapa E2) — una consulta SELECT
        // sin SESSION_CONTEXT no vería la fila aunque el INSERT haya
        // funcionado. Por eso la prueba no confirma con un COUNT posterior:
        // que SaveChangesAsync no lance ya demuestra que la CHECK aceptó
        // el valor, que es exactamente lo que UT-E3.1 pide verificar.
        await using var ctx = CrearContexto();
        ctx.Puestos.Add(new Puesto
        {
            LineaId = 1, Codigo = "U3", NombrePuesto = "Prueba", Tipo = "rotativo",
            HorasEnPuesto = 5, UmbralCriticoHoras = 8,
        });

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().NotThrowAsync();
    }

    [Fact]
    public async Task CK_Puesto_umbrales_acepta_ambos_nulos_o_solo_uno_presente()
    {
        // A4/A14: los umbrales se siembran vacíos — nulo no es un error,
        // es el estado de partida hasta calibrar con operación real.
        await using var ctx = CrearContexto();
        ctx.Puestos.AddRange(
            new Puesto { LineaId = 1, Codigo = "U4", NombrePuesto = "Sin ninguno", Tipo = "fijo", CategoriaTitular = "averiero" },
            new Puesto { LineaId = 1, Codigo = "U5", NombrePuesto = "Solo sugerido", Tipo = "rotativo", HorasEnPuesto = 2 });

        var accion = async () => await ctx.SaveChangesAsync();
        await accion.Should().NotThrowAsync();
    }

    // ═══ E3.3 — Personal.Sexo nulable = "no evaluar" (00 §A13, §7.3) ═══

    [Fact]
    public async Task Personal_se_guarda_con_sexo_nulo_sin_error()
    {
        await using var ctx = CrearContexto();
        ctx.Personas.Add(new Personal { Ficha = "F100", NombreCompleto = "Sin sexo registrado", Categoria = "operario", Sexo = null });

        await ctx.SaveChangesAsync();

        var guardado = await ctx.Personas.SingleAsync(p => p.Ficha == "F100");
        guardado.Sexo.Should().BeNull("00 §A13/§7.3: nulo significa 'no evaluar', el motor de E4 no debe inferir un valor");
    }

    // ═══ E3.4 — DENY DELETE sobre RestriccionMedica (00 §C14, 04 §7.5) ═══

    [Fact]
    public async Task DENY_impide_borrar_una_restriccion_medica_con_la_cuenta_de_aplicacion()
    {
        int usuarioId, personalId;
        await using (var ctx = CrearContexto())
        {
            var usuario = new Usuario { Username = "coord_rm", NombreCompleto = "Coordinador", Rol = "coordinador", OrigenIdentidad = "local", Activo = true };
            var personal = new Personal { Ficha = "F200", NombreCompleto = "Trabajador con restricción", Categoria = "operario" };
            ctx.Usuarios.Add(usuario);
            ctx.Personas.Add(personal);
            await ctx.SaveChangesAsync();

            ctx.RestriccionesMedicas.Add(new RestriccionMedica
            {
                PersonalId = personal.Id, CapacidadId = 1,
                FechaInicio = new DateOnly(2026, 1, 1), FechaDictamen = new DateOnly(2026, 1, 1),
                Fuente = "Enfermería", RegistradoPor = usuario.Id,
            });
            await ctx.SaveChangesAsync();
            usuarioId = usuario.Id;
            personalId = personal.Id;
        }

        // rol_app necesita SELECT/INSERT/UPDATE para operar con normalidad
        // — lo único que 04 §7.5 le niega es DELETE. Sin estos grants la
        // prueba no distinguiría "no tiene permiso de nada" de "tiene
        // permiso de todo menos borrar", que es la garantía que importa.
        await using (var conexionAdmin = new SqlConnection(CadenaConexion))
        {
            await conexionAdmin.OpenAsync();
            await using var cmd = conexionAdmin.CreateCommand();
            cmd.CommandText = "GRANT SELECT, INSERT, UPDATE ON dbo.RestriccionMedica TO rol_app;";
            await cmd.ExecuteNonQueryAsync();
        }

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var impersonar = conexion.CreateCommand();
        impersonar.CommandText = "EXECUTE AS USER = 'rol_app';";
        await impersonar.ExecuteNonQueryAsync();

        await using var borrar = conexion.CreateCommand();
        borrar.CommandText = "DELETE FROM RestriccionMedica WHERE personal_id = @p";
        borrar.Parameters.AddWithValue("@p", personalId);

        var accion = async () => await borrar.ExecuteNonQueryAsync();
        var excepcion = await accion.Should().ThrowAsync<SqlException>();
        excepcion.Which.Message.Should().Contain("DELETE permission was denied");

        await using var revertir = conexion.CreateCommand();
        revertir.CommandText = "REVERT;";
        await revertir.ExecuteNonQueryAsync();

        // La fila sigue ahí — el intento denegado no dejó nada a medias.
        await using var ctxVerificacion = CrearContexto();
        (await ctxVerificacion.RestriccionesMedicas.CountAsync(r => r.PersonalId == personalId)).Should().Be(1);
    }
}
