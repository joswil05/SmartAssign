using System.Data;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Tiempo;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// Revisión de producción, hallazgo <b>P-01</b>: seis procedimientos vivos
/// derivaban el día de calendario de <c>SYSUTCDATETIME()</c>, y
/// <c>sp_ValidarAsignacion</c> le pasaba esa fecha a
/// <c>fn_TieneRestriccionBloqueante</c> — la regla dura del §7.2 que
/// "NUNCA cede". Con el servidor en UTC−6, desde las 18:00 la fecha UTC ya
/// es la de mañana, y una restricción médica con <c>fecha_fin = hoy</c>
/// dejaba de bloquear seis horas antes de tiempo.
///
/// <b>El problema de probar esto:</b> el fallo solo se manifiesta durante
/// la ventana en que las dos fechas difieren (seis horas al día), así que
/// una prueba que dependa de la hora de ejecución es inútil — pasaría en
/// verde toda la mañana. Las de aquí no dependen de la hora:
/// <see cref="La_regla_medica_evalua_con_la_fecha_de_la_planta"/> construye
/// el caso peligroso relativo a la fecha de planta, y
/// <see cref="Ningun_modulo_del_esquema_deriva_una_fecha_de_UTC"/> vigila
/// el mecanismo directamente sobre el esquema desplegado.
/// </summary>
public class FechaDePlantaTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await ctx.Database.OpenConnectionAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
    }

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

    // ── El mecanismo ────────────────────────────────────────────────────

    [Fact]
    public async Task fn_FechaPlanta_da_la_fecha_local_del_servidor_no_la_UTC()
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        var fila = await conexion.QuerySingleAsync<(DateTime Planta, DateTime Local, DateTime Utc)>(
            "SELECT dbo.fn_FechaPlanta() AS Planta, CAST(SYSDATETIME() AS DATE) AS Local, CAST(SYSUTCDATETIME() AS DATE) AS Utc;");

        DateOnly.FromDateTime(fila.Planta).Should().Be(DateOnly.FromDateTime(fila.Local),
            "00 §C6: la hora es siempre la del servidor, y el servidor está en la planta");

        // No se afirma que difieran de la UTC: dependería de la hora a la
        // que corra la prueba. Lo que sí se afirma es que la de planta
        // sigue a la LOCAL, que es lo que estaba mal.
    }

    [Fact]
    public async Task El_espejo_en_C_sostiene_la_misma_fecha_que_el_de_SQL()
    {
        // FechaPlanta.Hoy() y dbo.fn_FechaPlanta() tienen que decir lo
        // mismo: si divergen, la malla y el motor discreparían sobre qué
        // restricciones están vigentes.
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        var enSql = await conexion.QuerySingleAsync<DateTime>("SELECT dbo.fn_FechaPlanta();");

        DateOnly.FromDateTime(enSql).Should().Be(FechaPlanta.Hoy());
    }

    [Fact]
    public async Task Ningun_modulo_del_esquema_deriva_una_fecha_de_UTC()
    {
        // La guarda contra la reaparición. Un procedimiento nuevo que
        // vuelva a escribir CAST(SYSUTCDATETIME() AS DATE) rompe aquí,
        // nombrándose. Los INSTANTES en UTC no se tocan — solo se persigue
        // la conversión de instante a DÍA DE CALENDARIO.
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        var culpables = (await conexion.QueryAsync<string>("""
            SELECT o.name
              FROM sys.sql_modules m
              JOIN sys.objects   o ON o.object_id = m.object_id
             WHERE (m.definition LIKE '%CAST(SYSUTCDATETIME() AS DATE)%'
                 OR m.definition LIKE '%CONVERT(DATE, SYSUTCDATETIME())%')
               AND o.name <> 'fn_FechaPlanta'
            """)).ToList();

        culpables.Should().BeEmpty(
            "un día de calendario se saca de dbo.fn_FechaPlanta(), nunca de la hora UTC (P-01)");
    }

    // ── La consecuencia real, que es de lo que trata el hallazgo ────────

    [Fact]
    public async Task La_regla_medica_evalua_con_la_fecha_de_la_planta()
    {
        // El caso exacto que fallaba: un dictamen que vence HOY en la
        // planta. Antes del arreglo, durante la ventana de divergencia
        // sp_ValidarAsignacion comparaba contra la fecha de mañana, veía
        // fecha_fin < @fecha y dejaba pasar la asignación.
        int personalId, puestoId, usuarioId;

        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);

            var usuario = new Usuario
            {
                Username = "u_fecha", NombreCompleto = "Coordinador",
                Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
            };
            ctx.Usuarios.Add(usuario);
            await ctx.SaveChangesAsync();
            usuarioId = usuario.Id;

            var persona = new Personal
            {
                Ficha = "F-FECHA-1", NombreCompleto = "Persona con dictamen que vence hoy",
                Categoria = "operario", LineaHabitual = 1,
                Situacion = "presente_sin_asignar", LineaFisicaActual = 1,
            };
            ctx.Personas.Add(persona);

            var puesto = new Puesto
            {
                LineaId = 1, Codigo = "P-FECHA", NombrePuesto = "Puesto que exige la capacidad",
                Tipo = "rotativo", HorasEnPuesto = 4,
            };
            ctx.Puestos.Add(puesto);
            await ctx.SaveChangesAsync();
            personalId = persona.Id;
            puestoId = puesto.Id;

            var capacidad = await ctx.CapacidadesFisicas.FirstAsync();
            ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidad.Id });

            // Vigente HASTA HOY inclusive, en fecha de planta.
            var hoyEnPlanta = FechaPlanta.Hoy();
            ctx.RestriccionesMedicas.Add(new RestriccionMedica
            {
                PersonalId = personalId,
                CapacidadId = capacidad.Id,
                FechaInicio = hoyEnPlanta.AddDays(-10),
                FechaFin = hoyEnPlanta,
                Fuente = "Prueba P-01 — dictamen que vence hoy",
                FechaDictamen = hoyEnPlanta.AddDays(-10),
                RegistradoPor = usuarioId,
            });
            await ctx.SaveChangesAsync();
        }

        var (codigo, _) = await ValidarAsync(personalId, puestoId, usuarioId);

        codigo.Should().Be("RESTRICCION_MEDICA",
            "§7.2 es regla dura: un dictamen vigente hasta hoy bloquea durante TODO el día de la planta, "
            + "no hasta que en Londres sea medianoche");
    }

    [Fact]
    public async Task Un_dictamen_que_vencio_ayer_sigue_sin_bloquear()
    {
        // El otro lado, para que el arreglo no se pase de frenada: el
        // escenario 2 de la semilla adversaria (07 §4.4) tiene que seguir
        // comportándose igual.
        int personalId, puestoId, usuarioId;

        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);

            var usuario = new Usuario
            {
                Username = "u_fecha2", NombreCompleto = "Coordinador",
                Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
            };
            ctx.Usuarios.Add(usuario);
            await ctx.SaveChangesAsync();
            usuarioId = usuario.Id;

            var persona = new Personal
            {
                Ficha = "F-FECHA-2", NombreCompleto = "Persona con dictamen vencido",
                Categoria = "operario", LineaHabitual = 1,
                Situacion = "presente_sin_asignar", LineaFisicaActual = 1,
            };
            ctx.Personas.Add(persona);

            var puesto = new Puesto
            {
                LineaId = 1, Codigo = "P-FECHA2", NombrePuesto = "Puesto que exige la capacidad",
                Tipo = "rotativo", HorasEnPuesto = 4,
            };
            ctx.Puestos.Add(puesto);
            await ctx.SaveChangesAsync();
            personalId = persona.Id;
            puestoId = puesto.Id;

            var capacidad = await ctx.CapacidadesFisicas.FirstAsync();
            ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidad.Id });

            var hoyEnPlanta = FechaPlanta.Hoy();
            ctx.RestriccionesMedicas.Add(new RestriccionMedica
            {
                PersonalId = personalId,
                CapacidadId = capacidad.Id,
                FechaInicio = hoyEnPlanta.AddDays(-30),
                FechaFin = hoyEnPlanta.AddDays(-1),
                Fuente = "Prueba P-01 — dictamen vencido ayer",
                FechaDictamen = hoyEnPlanta.AddDays(-30),
                RegistradoPor = usuarioId,
            });
            await ctx.SaveChangesAsync();
        }

        var (codigo, _) = await ValidarAsync(personalId, puestoId, usuarioId);

        codigo.Should().NotBe("RESTRICCION_MEDICA", "venció ayer: ya no aplica");
    }

    private async Task<(string? Codigo, string? Mensaje)> ValidarAsync(int personalId, int puestoId, int usuarioId)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();

        await using (var contexto = conexion.CreateCommand())
        {
            contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
            await contexto.ExecuteNonQueryAsync();
        }

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ValidarAsignacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@permitir_ceder_perfil", 0);
        cmd.Parameters.AddWithValue("@es_liderazgo_manual", 0);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", SqlDbType.NVarChar, 400) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);

        await cmd.ExecuteNonQueryAsync();

        return (pCodigo.Value as string, pMensaje.Value as string);
    }
}
