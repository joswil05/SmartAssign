using System.Diagnostics;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E14.4 (docs/PROGRESO.md), continuación de <see cref="RendimientoContraPresupuestosTests"/>:
/// "Barrido de puestos fijos" — objetivo &lt; 10 s / máximo 20 s (05_TRD.md
/// §3.4). En su propia clase, con su propio <c>SmartAssignApiFactory</c>
/// (base de datos propia): necesita las 10 líneas reales planificadas a
/// la vez para medir "a escala real" (05 §3.4, literal: "10 líneas ·
/// ~160 trabajadores · unos 300 puestos"), y compartir base de datos con
/// el resto de presupuestos (que también planifican líneas sueltas)
/// chocaría contra <c>UX_JornadaLinea_abierta</c> — se descubrió así, no
/// se anticipó: el primer borrador de esta prueba vivía en la misma
/// clase que las demás y fallaba justo por eso.
///
/// <c>sp_ArrancarTurno</c> (E5.7) es el único punto de entrada real al
/// barrido (C12) — arranca las 10 líneas en una sola llamada, exactamente
/// como ocurre una vez por día en producción; por eso se mide UNA
/// corrida, no un percentil sobre muchas (arrancar turno no se repite
/// varias veces por turno).
/// </summary>
public class RendimientoBarridoDeEscalaTests(SmartAssignApiFactory factory) : IClassFixture<SmartAssignApiFactory>
{
    private async Task<(int usuarioId, string username, string password)> CrearUsuarioAsync(string rol, string prefijo)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
        var credenciales = new ServicioCredenciales();
        var password = $"Clave#{Guid.NewGuid():N}"[..16];
        var (hash, salt) = credenciales.HashConSal(password);
        var username = $"{prefijo}_{Guid.NewGuid():N}"[..30];

        var usuario = new Usuario
        {
            Username = username, NombreCompleto = username, Rol = rol, OrigenIdentidad = "local",
            PasswordHash = hash, PasswordSalt = salt, Activo = true,
        };
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return (usuario.Id, username, password);
    }

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(factory.CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
    }

    private async Task PlanificarLineaAsync(byte lineaId, byte turnoId, DateOnly dia, int skuId, int supervisorId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_PlanificarLinea";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@sku_id", skuId);
        cmd.Parameters.AddWithValue("@supervisor_id", supervisorId); // UX_Linea_supervisor: cada línea necesita su PROPIO supervisor, nunca el mismo usuario repetido.
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.Add(new SqlParameter("@jornada_linea_id", System.Data.SqlDbType.Int) { Direction = System.Data.ParameterDirection.Output });
        var pRechazo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pRechazo);
        await cmd.ExecuteNonQueryAsync();
        (pRechazo.Value as string).Should().BeNull($"sp_PlanificarLinea no debe rechazar en el fixture de rendimiento (línea {lineaId})");
    }

    private async Task ConfirmarPlanificacionAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ConfirmarPlanificacion";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pRechazo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pRechazo);
        cmd.Parameters.Add(new SqlParameter("@lineas_sin_supervisor", System.Data.SqlDbType.VarChar, 200) { Direction = System.Data.ParameterDirection.Output });
        await cmd.ExecuteNonQueryAsync();
        (pRechazo.Value as string).Should().BeNull($"sp_ConfirmarPlanificacion no debe rechazar en el fixture de rendimiento (turno {turnoId})");
    }

    private async Task ArrancarTurnoAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ArrancarTurno";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.CommandTimeout = 60; // barrido a escala real: más margen que el default de 30 s del arnés.
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pRechazo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pRechazo);
        await cmd.ExecuteNonQueryAsync();
        (pRechazo.Value as string).Should().BeNull($"sp_ArrancarTurno no debe rechazar en el fixture de rendimiento (turno {turnoId})");
    }

    [Fact]
    public async Task Barrido_de_puestos_fijos_a_escala_real_cumple_su_presupuesto()
    {
        var coord = await CrearUsuarioAsync("coordinador", "coord_rend_barrido");

        byte turno;
        DateOnly dia = new(2026, 8, 12);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();
            var t = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
            db.Turnos.Add(t);
            await db.SaveChangesAsync();
            turno = t.Id;

            // ~160 trabajadores: 100 operador_b (candidatas reales del
            // barrido, ver sp_BarridoPuestosFijos) + 60 de otras
            // categorías, para que la escala de personal sea honesta
            // aunque no todas participen en este mecanismo específico.
            for (var i = 0; i < 100; i++)
                db.Personas.Add(new Personal { Ficha = $"OB{i}_{Guid.NewGuid():N}"[..12], NombreCompleto = $"Operador B {i}", Categoria = "operador_b", Situacion = "presente_sin_asignar" });
            for (var i = 0; i < 60; i++)
                db.Personas.Add(new Personal { Ficha = $"OP{i}_{Guid.NewGuid():N}"[..12], NombreCompleto = $"Operario {i}", Categoria = "operario", Situacion = "presente_sin_asignar" });
            await db.SaveChangesAsync();

            // ~300 puestos: 10 fijos + 20 rotativos por línea, en las 10
            // líneas reales (PrioridadLinea ya sembrada desde E0 para
            // todas). titular_id NULL a propósito — sp_BarridoPuestosFijos
            // recorre entonces la vía del suplente para cada uno, que es
            // el camino computacionalmente real que este presupuesto mide.
            for (byte linea = 1; linea <= 10; linea++)
            {
                for (var i = 0; i < 10; i++)
                    db.Puestos.Add(new Puesto { LineaId = linea, Codigo = $"L{linea}F{i}_{Guid.NewGuid():N}"[..15], NombrePuesto = $"Fijo {i}", Tipo = "fijo" });
                for (var i = 0; i < 20; i++)
                    db.Puestos.Add(new Puesto { LineaId = linea, Codigo = $"L{linea}R{i}_{Guid.NewGuid():N}"[..15], NombrePuesto = $"Rotativo {i}", Tipo = "rotativo" });

                var sku = new Sku { Codigo = $"SKUB{linea}_{Guid.NewGuid():N}"[..15], Descripcion = "SKU de rendimiento", RitmoTeoricoHora = 100 };
                db.Skus.Add(sku);
                await db.SaveChangesAsync();

                var (supervisorId, _, _) = await CrearUsuarioAsync("supervisor", $"sup_rend_barrido_l{linea}");
                await PlanificarLineaAsync(linea, turno, dia, sku.Id, supervisorId, coord.usuarioId);
            }
        }

        await ConfirmarPlanificacionAsync(turno, dia, coord.usuarioId);

        var cronometro = Stopwatch.StartNew();
        await ArrancarTurnoAsync(turno, dia, coord.usuarioId);
        cronometro.Stop();

        cronometro.Elapsed.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(20),
            $"Barrido de puestos fijos a escala real: {cronometro.Elapsed.TotalSeconds:F1} s no debe superar el máximo del presupuesto (20 s)");
        cronometro.Elapsed.Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(10),
            $"Barrido de puestos fijos a escala real: {cronometro.Elapsed.TotalSeconds:F1} s no debe superar el objetivo del presupuesto (10 s)");
    }
}
