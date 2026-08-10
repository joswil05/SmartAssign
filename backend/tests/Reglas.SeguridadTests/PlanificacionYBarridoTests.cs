using System.Data;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E5.1 a E5.7 (docs/PROGRESO.md): <c>Turno</c> y el resto de
/// <c>JornadaLinea</c> (04 §4.1), el cambio de prioridad versionado (00
/// §B8), <c>fn_PuestoFueraDeOperacion</c>/<c>fn_EsVacanteCritica</c>/
/// <c>fn_SituacionPuesto</c> (§5.3), la planificación con rechazo si
/// falta supervisor (§8.1), <c>sp_BarridoPuestosFijos</c> por prioridad
/// (§8.3, A9) y la ventana de arranque por jornada-línea (§8.4).
///
/// Las 10 líneas y la prioridad base (L4&gt;L1&gt;L2&gt;L6&gt;L7&gt;L5&gt;L3&gt;L8&gt;L9&gt;L10)
/// ya vienen sembradas (E1) — estas pruebas las reutilizan en vez de
/// crear líneas nuevas, igual que el resto de la suite.
/// </summary>
public class PlanificacionYBarridoTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != ConnectionState.Open) await conexion.OpenAsync();
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

    // ═══ Helpers de datos ═══

    private static async Task<int> CrearUsuarioAsync(SmartAssignDbContext ctx, string rol = "coordinador")
    {
        var u = new Usuario
        {
            Username = $"u_{Guid.NewGuid():N}"[..15], NombreCompleto = "Usuario de prueba",
            Rol = rol, OrigenIdentidad = "local", Activo = true,
        };
        ctx.Usuarios.Add(u);
        await ctx.SaveChangesAsync();
        return u.Id;
    }

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria,
        byte? lineaFisicaActual = null, string situacion = "presente_sin_asignar")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12],
            NombreCompleto = "Persona de prueba",
            Categoria = categoria,
            LineaFisicaActual = lineaFisicaActual,
            Situacion = situacion,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, byte lineaId, string tipo,
        string? categoriaTitular = null, string? perfilRequerido = null, int? titularId = null, bool activo = true)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId,
            Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba",
            Tipo = tipo,
            CategoriaTitular = categoriaTitular,
            PerfilRequerido = perfilRequerido,
            TitularId = titularId,
            Activo = activo,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    private static async Task<byte> CrearTurnoAsync(SmartAssignDbContext ctx, TimeOnly inicio, TimeOnly fin)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = inicio, HoraFin = fin };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        return turno.Id;
    }

    private static async Task<int> CrearSkuAsync(SmartAssignDbContext ctx)
    {
        var sku = new Sku { Codigo = $"SKU{Guid.NewGuid():N}"[..15], Descripcion = "SKU de prueba", RitmoTeoricoHora = 100 };
        ctx.Skus.Add(sku);
        await ctx.SaveChangesAsync();
        return sku.Id;
    }

    private static async Task SetParametroAsync(SmartAssignDbContext ctx, string clave, string valor)
    {
        ctx.Parametros.Add(new Parametro { Clave = clave, Valor = valor, Tipo = "int", Descripcion = "prueba" });
        await ctx.SaveChangesAsync();
    }

    // ═══ Helpers de invocación SQL cruda ═══

    private async Task<SqlConnection> AbrirComoCoordinadorAsync()
    {
        var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await cmd.ExecuteNonQueryAsync();
        return conexion;
    }

    private async Task<bool> FnBitAsync(string nombreFuncion, params (string nombre, object? valor)[] parametros)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        var lista = string.Join(", ", parametros.Select(p => p.nombre));
        cmd.CommandText = $"SELECT dbo.{nombreFuncion}({lista})";
        foreach (var (nombre, valor) in parametros) cmd.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);
        var resultado = await cmd.ExecuteScalarAsync();
        return resultado switch { bool b => b, byte n => n == 1, _ => false };
    }

    private async Task<string> FnSituacionPuestoAsync(int puestoId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT dbo.fn_SituacionPuesto(@p)";
        cmd.Parameters.AddWithValue("@p", puestoId);
        return (string)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task<string?> CambiarPrioridadAsync(byte lineaId, byte ordenNuevo, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_CambiarPrioridadLinea";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@orden_nuevo", ordenNuevo);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    private async Task<(int? JornadaLineaId, string? Codigo)> PlanificarLineaAsync(
        byte lineaId, byte turnoId, DateOnly dia, int? skuId, int? supervisorId, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_PlanificarLinea";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@linea_id", lineaId);
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@sku_id", (object?)skuId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@supervisor_id", (object?)supervisorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pId = new SqlParameter("@jornada_linea_id", SqlDbType.Int) { Direction = ParameterDirection.Output };
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pId);
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return (pId.Value as int?, pCodigo.Value as string);
    }

    private async Task<(string? Codigo, string? LineasSinSupervisor)> ConfirmarPlanificacionAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ConfirmarPlanificacion";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        var pLineas = new SqlParameter("@lineas_sin_supervisor", SqlDbType.VarChar, 200) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pLineas);
        await cmd.ExecuteNonQueryAsync();
        return (pCodigo.Value as string, pLineas.Value as string);
    }

    private async Task<string?> ArrancarTurnoAsync(byte turnoId, DateOnly dia, int usuarioId)
    {
        await using var conexion = await AbrirComoCoordinadorAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ArrancarTurno";
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@turno_id", turnoId);
        cmd.Parameters.AddWithValue("@dia_operacion", dia.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        var pCodigo = new SqlParameter("@codigo_rechazo", SqlDbType.VarChar, 40) { Direction = ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        await cmd.ExecuteNonQueryAsync();
        return pCodigo.Value as string;
    }

    // ═══ E5.1 — Turno y JornadaLinea (04 §4.1, 00 §C6) ═══

    [Fact]
    public async Task Turno_normal_no_cruza_medianoche()
    {
        await using var ctx = CrearContexto();
        var id = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));

        var turno = await ctx.Turnos.SingleAsync(t => t.Id == id);
        turno.CruzaMedianoche.Should().BeFalse();
    }

    [Fact]
    public async Task Turno_que_cruza_medianoche_queda_marcado()
    {
        // 00 §C6: "un turno que cruza medianoche pertenece ENTERO a su fecha de inicio".
        await using var ctx = CrearContexto();
        var id = await CrearTurnoAsync(ctx, new TimeOnly(22, 0), new TimeOnly(6, 0));

        var turno = await ctx.Turnos.SingleAsync(t => t.Id == id);
        turno.CruzaMedianoche.Should().BeTrue();
    }

    [Fact]
    public async Task No_se_puede_planificar_dos_veces_la_misma_linea_turno_dia()
    {
        // UQ_Jornada (04 §4.1) a nivel de base, sin pasar por el SP.
        await using var ctx = CrearContexto();
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var dia = new DateOnly(2026, 8, 10);

        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = dia });
        await ctx.SaveChangesAsync();

        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = dia });
        var act = async () => await ctx.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    // ═══ E5.2 — sp_CambiarPrioridadLinea (04 §2.2, 00 §B8) ═══

    [Fact]
    public async Task Cambiar_prioridad_intercambia_dos_lineas_sin_tocar_las_demas()
    {
        // Base sembrada (E1): L4=orden1, L1=orden2. Mover L1 a orden1 debe
        // intercambiarla con L4 — nunca desplazar en cascada a las otras 8.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var codigo = await CambiarPrioridadAsync(lineaId: 1, ordenNuevo: 1, usuario);
        codigo.Should().BeNull();

        var l1 = await ctx.PrioridadesLinea.SingleAsync(p => p.LineaId == 1 && p.VigenteHasta == null);
        var l4 = await ctx.PrioridadesLinea.SingleAsync(p => p.LineaId == 4 && p.VigenteHasta == null);
        l1.Orden.Should().Be((byte)1);
        l4.Orden.Should().Be((byte)2);
        l1.CambiadoPor.Should().Be(usuario);

        // Las demás 8 líneas conservan su orden original.
        (await ctx.PrioridadesLinea.SingleAsync(p => p.LineaId == 2 && p.VigenteHasta == null)).Orden.Should().Be((byte)3);
        (await ctx.PrioridadesLinea.SingleAsync(p => p.LineaId == 10 && p.VigenteHasta == null)).Orden.Should().Be((byte)10);
    }

    [Fact]
    public async Task Cambiar_prioridad_es_solo_hacia_adelante_nunca_reescribe_el_historico()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        await CambiarPrioridadAsync(lineaId: 1, ordenNuevo: 1, usuario);

        var filaCerrada = await ctx.PrioridadesLinea.SingleAsync(p => p.LineaId == 1 && p.VigenteHasta != null);
        filaCerrada.Orden.Should().Be((byte)2, "B8: la fila cerrada conserva el valor que tuvo, nunca se reescribe");
        filaCerrada.VigenteHasta.Should().NotBeNull();
    }

    [Fact]
    public async Task Cambiar_prioridad_fuera_de_rango_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var codigo = await CambiarPrioridadAsync(lineaId: 1, ordenNuevo: 11, usuario);

        codigo.Should().Be("ORDEN_FUERA_DE_RANGO");
        (await ctx.PrioridadesLinea.CountAsync(p => p.VigenteHasta == null)).Should().Be(10, "nada debe cambiar");
    }

    [Fact]
    public async Task Cambiar_prioridad_sin_cambio_real_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        var codigo = await CambiarPrioridadAsync(lineaId: 4, ordenNuevo: 1, usuario); // ya es 1

        codigo.Should().Be("SIN_CAMBIO");
    }

    [Fact]
    public async Task Cambiar_prioridad_de_linea_sin_vigente_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);

        // Cierra la fila vigente de L1 a mano, sin abrir otra — estado excepcional.
        await using (var conexion = await AbrirComoCoordinadorAsync())
        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = "UPDATE PrioridadLinea SET vigente_hasta = SYSUTCDATETIME() WHERE linea_id = 1 AND vigente_hasta IS NULL;";
            await cmd.ExecuteNonQueryAsync();
        }

        var codigo = await CambiarPrioridadAsync(lineaId: 1, ordenNuevo: 5, usuario);

        codigo.Should().Be("LINEA_SIN_PRIORIDAD_VIGENTE");
    }

    // ═══ E5.3 — fn_PuestoFueraDeOperacion (04 §2.5, §5.3) ═══

    [Fact]
    public async Task Sin_jornada_abierta_el_puesto_esta_fuera_de_operacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo");

        (await FnBitAsync("fn_PuestoFueraDeOperacion", ("@p", puesto))).Should().BeTrue();
    }

    [Fact]
    public async Task Jornada_sin_sku_linea_inactiva_deja_el_puesto_fuera_de_operacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo");
        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 8, 10), SkuId = null });
        await ctx.SaveChangesAsync();

        (await FnBitAsync("fn_PuestoFueraDeOperacion", ("@p", puesto))).Should().BeTrue("§8.1: sin SKU, la línea queda inactiva");
    }

    [Fact]
    public async Task Puesto_sin_filas_en_PuestoSKU_nunca_esta_fuera_de_operacion_por_sku()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo");
        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 8, 10), SkuId = sku });
        await ctx.SaveChangesAsync();

        (await FnBitAsync("fn_PuestoFueraDeOperacion", ("@p", puesto))).Should().BeFalse(
            "04 §2.5: sin fila en PuestoSKU, el puesto no depende del SKU");
    }

    [Fact]
    public async Task Puesto_con_el_sku_vigente_declarado_no_esta_fuera_de_operacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo");
        ctx.PuestosSku.Add(new PuestoSku { PuestoId = puesto, SkuId = sku });
        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 8, 10), SkuId = sku });
        await ctx.SaveChangesAsync();

        (await FnBitAsync("fn_PuestoFueraDeOperacion", ("@p", puesto))).Should().BeFalse();
    }

    [Fact]
    public async Task Puesto_gated_a_otro_sku_distinto_del_vigente_esta_fuera_de_operacion()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var skuDeclarado = await CrearSkuAsync(ctx);
        var skuVigente = await CrearSkuAsync(ctx);
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo");
        ctx.PuestosSku.Add(new PuestoSku { PuestoId = puesto, SkuId = skuDeclarado });
        ctx.JornadasLinea.Add(new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 8, 10), SkuId = skuVigente });
        await ctx.SaveChangesAsync();

        (await FnBitAsync("fn_PuestoFueraDeOperacion", ("@p", puesto))).Should().BeTrue(
            "el puesto solo declara otro SKU, no el que está corriendo hoy");
    }

    // ═══ E5.4 — Planificación (§8.1, 02 §3.1) ═══

    [Fact]
    public async Task Planificar_con_sku_deja_la_linea_activa()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);

        var (jornadaId, codigo) = await PlanificarLineaAsync(1, turno, new DateOnly(2026, 8, 10), sku, supervisorId: null, usuario);

        codigo.Should().BeNull();
        jornadaId.Should().NotBeNull();
        var jornada = await ctx.JornadasLinea.SingleAsync(j => j.Id == jornadaId);
        jornada.Estado.Should().Be("planificada");
        jornada.SkuId.Should().Be(sku);
    }

    [Fact]
    public async Task Planificar_sin_sku_marca_la_linea_inactiva_y_no_exige_supervisor()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var dia = new DateOnly(2026, 8, 10);

        await PlanificarLineaAsync(3, turno, dia, skuId: null, supervisorId: null, usuario);

        var (codigo, _) = await ConfirmarPlanificacionAsync(turno, dia, usuario);
        codigo.Should().BeNull("§8.1: una línea sin SKU queda inactiva y no necesita supervisor");
    }

    [Fact]
    public async Task Replanificar_antes_de_confirmar_actualiza_en_vez_de_duplicar()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku1 = await CrearSkuAsync(ctx);
        var sku2 = await CrearSkuAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);

        var (id1, _) = await PlanificarLineaAsync(1, turno, dia, sku1, null, usuario);
        var (id2, _) = await PlanificarLineaAsync(1, turno, dia, sku2, null, usuario);

        id1.Should().Be(id2);
        (await ctx.JornadasLinea.CountAsync()).Should().Be(1);
        (await ctx.JornadasLinea.SingleAsync()).SkuId.Should().Be(sku2);
    }

    [Fact]
    public async Task Confirmar_rechaza_nominalmente_si_falta_supervisor()
    {
        // Ejemplo normativo del flujo (02 §3.1): "L4 y L7 están activas sin supervisor asignado."
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);

        await PlanificarLineaAsync(4, turno, dia, sku, supervisorId: null, usuario);
        await PlanificarLineaAsync(7, turno, dia, sku, supervisorId: null, usuario);
        await PlanificarLineaAsync(1, turno, dia, sku, usuario, usuario); // ésta sí tiene supervisor (cualquier Usuario.Id sirve para la FK)

        var (codigo, lineas) = await ConfirmarPlanificacionAsync(turno, dia, usuario);

        codigo.Should().Be("FALTA_SUPERVISOR");
        lineas.Should().Be("L4, L7");
    }

    [Fact]
    public async Task Confirmar_con_todo_supervisor_asignado_pasa_a_confirmada_y_sincroniza_el_supervisor_en_vivo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var supervisor = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);

        await PlanificarLineaAsync(1, turno, dia, sku, supervisor, usuario);

        var (codigo, lineasSinSupervisor) = await ConfirmarPlanificacionAsync(turno, dia, usuario);

        codigo.Should().BeNull();
        lineasSinSupervisor.Should().BeNull();
        (await ctx.JornadasLinea.SingleAsync()).Estado.Should().Be("confirmada");
        var linea = await ctx.Lineas.AsNoTracking().SingleAsync(l => l.Id == 1);
        linea.SupervisorActualId.Should().Be(supervisor, "04 §6.1, D6: el aislamiento en vivo depende de este campo");
        linea.Situacion.Should().Be("activa");
    }

    [Fact]
    public async Task Confirmar_libera_al_supervisor_de_su_linea_anterior_al_moverlo()
    {
        // UX_Linea_supervisor: un supervisor no puede tener dos líneas.
        // Lecturas con AsNoTracking(): Linea(1) se lee dos veces en esta
        // prueba, antes y después de que un SP externo la modifique — sin
        // eso, EF devolvería la instancia ya rastreada (con el valor
        // viejo en memoria) en vez de volver a consultar la base.
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var supervisor = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);

        await PlanificarLineaAsync(1, turno, new DateOnly(2026, 8, 9), sku, supervisor, usuario);
        await ConfirmarPlanificacionAsync(turno, new DateOnly(2026, 8, 9), usuario);
        (await ctx.Lineas.AsNoTracking().SingleAsync(l => l.Id == 1)).SupervisorActualId.Should().Be(supervisor);

        await PlanificarLineaAsync(2, turno, new DateOnly(2026, 8, 10), sku, supervisor, usuario);
        var (codigo, _) = await ConfirmarPlanificacionAsync(turno, new DateOnly(2026, 8, 10), usuario);

        codigo.Should().BeNull();
        (await ctx.Lineas.AsNoTracking().SingleAsync(l => l.Id == 1)).SupervisorActualId.Should().BeNull("se liberó al moverlo a L2");
        (await ctx.Lineas.AsNoTracking().SingleAsync(l => l.Id == 2)).SupervisorActualId.Should().Be(supervisor);
    }

    [Fact]
    public async Task Confirmar_sin_ninguna_planificacion_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));

        var (codigo, _) = await ConfirmarPlanificacionAsync(turno, new DateOnly(2026, 8, 10), usuario);

        codigo.Should().Be("SIN_PLANIFICACION");
    }

    [Fact]
    public async Task Confirmar_dos_veces_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var dia = new DateOnly(2026, 8, 10);
        await PlanificarLineaAsync(1, turno, dia, skuId: null, supervisorId: null, usuario);
        await ConfirmarPlanificacionAsync(turno, dia, usuario);

        var (codigo, _) = await ConfirmarPlanificacionAsync(turno, dia, usuario);

        codigo.Should().Be("PLANIFICACION_YA_CONFIRMADA");
    }

    [Fact]
    public async Task No_se_puede_replanificar_una_linea_ya_confirmada()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var dia = new DateOnly(2026, 8, 10);
        await PlanificarLineaAsync(1, turno, dia, skuId: null, supervisorId: null, usuario);
        await ConfirmarPlanificacionAsync(turno, dia, usuario);

        var (_, codigo) = await PlanificarLineaAsync(1, turno, dia, skuId: null, supervisorId: null, usuario);

        codigo.Should().Be("PLANIFICACION_YA_CONFIRMADA", "§3.1: después de confirmada, cambiar de plan es una intervención (A6), no esta ruta");
    }

    // ═══ E5.5/E5.7 — sp_ArrancarTurno + sp_BarridoPuestosFijos (§8.3, §8.4) ═══

    private async Task<(byte Turno, int Sku, int Usuario)> PrepararTurnoConfirmadoAsync(SmartAssignDbContext ctx, byte lineaId, DateOnly dia, int? supervisorUsuarioId = null)
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        await PlanificarLineaAsync(lineaId, turno, dia, sku, supervisorUsuarioId ?? usuario, usuario);
        await ConfirmarPlanificacionAsync(turno, dia, usuario);
        return (turno, sku, usuario);
    }

    [Fact]
    public async Task Arrancar_asigna_al_titular_presente()
    {
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var titular = await CrearPersonaAsync(ctx, "operador_a", lineaFisicaActual: 1);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: titular);

        var codigo = await ArrancarTurnoAsync(turno, dia, usuario);

        codigo.Should().BeNull();
        var asignacion = await ctx.Asignaciones.SingleAsync(a => a.PuestoId == puesto);
        asignacion.PersonalId.Should().Be(titular);
        asignacion.TitularOriginalId.Should().BeNull("es el propio titular, no un suplente (§8.3, E5.6)");
        asignacion.Origen.Should().Be("barrido_automatico");
        (await FnSituacionPuestoAsync(puesto)).Should().Be("ocupado");
    }

    [Fact]
    public async Task Arrancar_cubre_con_operador_b_cuando_el_titular_esta_ausente_y_conserva_su_identidad()
    {
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var titular = await CrearPersonaAsync(ctx, "operador_a", lineaFisicaActual: 1, situacion: "ausente_justificado");
        var suplente = await CrearPersonaAsync(ctx, "operador_b", lineaFisicaActual: 1);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: titular);

        await ArrancarTurnoAsync(turno, dia, usuario);

        var asignacion = await ctx.Asignaciones.SingleAsync(a => a.PuestoId == puesto);
        asignacion.PersonalId.Should().Be(suplente);
        asignacion.TitularOriginalId.Should().Be(titular, "§8.3: el puesto registra las dos identidades");
    }

    [Fact]
    public async Task Arrancar_sin_titular_ni_operador_b_deja_vacante_critica()
    {
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: null);

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await ctx.Asignaciones.AnyAsync(a => a.PuestoId == puesto)).Should().BeFalse();
        (await FnBitAsync("fn_EsVacanteCritica", ("@p", puesto))).Should().BeTrue();
        (await FnSituacionPuestoAsync(puesto)).Should().Be("vacante_critica");
    }

    [Fact]
    public async Task Arrancar_respeta_la_restriccion_medica_del_titular_y_cae_al_operador_b()
    {
        // B12: la regla médica no cede en ningún motor, tampoco en el barrido automático.
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var titular = await CrearPersonaAsync(ctx, "operador_a", lineaFisicaActual: 1);
        var suplente = await CrearPersonaAsync(ctx, "operador_b", lineaFisicaActual: 1);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: titular);
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puesto, CapacidadId = 1 });
        ctx.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = titular, CapacidadId = 1,
            FechaInicio = new DateOnly(2020, 1, 1), FechaFin = null, FechaDictamen = new DateOnly(2020, 1, 1),
            Fuente = "Enfermería", RegistradoPor = usuario,
        });
        await ctx.SaveChangesAsync();

        await ArrancarTurnoAsync(turno, dia, usuario);

        var asignacion = await ctx.Asignaciones.SingleAsync(a => a.PuestoId == puesto);
        asignacion.PersonalId.Should().Be(suplente, "el titular está médicamente bloqueado — cae al mismo camino que 'ausente'");
        asignacion.TitularOriginalId.Should().Be(titular);
    }

    [Fact]
    public async Task Arrancar_nunca_toca_puestos_rotativos()
    {
        // C12: "Nunca genera asignación automática" en rotativos.
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, sku, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var titular = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 1);
        var puesto = await CrearPuestoAsync(ctx, 1, "rotativo", titularId: titular);
        ctx.PuestosSku.Add(new PuestoSku { PuestoId = puesto, SkuId = sku });
        await ctx.SaveChangesAsync();

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await ctx.Asignaciones.AnyAsync(a => a.PuestoId == puesto)).Should().BeFalse();
        (await FnSituacionPuestoAsync(puesto)).Should().Be("libre", "rotativo vacío al arrancar no es vacante crítica (C11)");
    }

    [Fact]
    public async Task Arrancar_no_toca_puestos_de_supervisor()
    {
        // §4.1: personal de liderazgo nunca se asigna automáticamente.
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: null, perfilRequerido: "Supervisor");

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await ctx.Asignaciones.AnyAsync(a => a.PuestoId == puesto)).Should().BeFalse();
        (await FnBitAsync("fn_EsVacanteCritica", ("@p", puesto))).Should().BeFalse(
            "el barrido ni siquiera lo intenta — no es una vacante que el motor deba resolver");
    }

    [Fact]
    public async Task Arrancar_salta_puestos_fuera_de_operacion()
    {
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, skuVigente, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var otroSku = await CrearSkuAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, "operador_a", lineaFisicaActual: 1);
        var puesto = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: titular);
        ctx.PuestosSku.Add(new PuestoSku { PuestoId = puesto, SkuId = otroSku }); // gated a un SKU que no es el vigente
        await ctx.SaveChangesAsync();

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await ctx.Asignaciones.AnyAsync(a => a.PuestoId == puesto)).Should().BeFalse();
        (await FnSituacionPuestoAsync(puesto)).Should().Be("fuera_de_operacion");
    }

    [Fact]
    public async Task Arrancar_recorre_las_lineas_por_prioridad_vigente_y_la_de_mayor_prioridad_se_lleva_al_unico_operador_b()
    {
        // §8.3: "los Operadores B disponibles son un recurso escaso... las
        // líneas más importantes reclaman primero" — la escasez es de
        // PLANTA, no de línea (ver nota de ingeniería en la migración).
        // L4 (orden 1) debe ganarle a L1 (orden 2) el único Operador B
        // disponible, aunque ambas líneas se arranquen en la misma llamada.
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var usuario = await CrearUsuarioAsync(ctx);
        var supervisorL4 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var supervisorL1 = await CrearUsuarioAsync(ctx, rol: "supervisor");
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);

        await PlanificarLineaAsync(4, turno, dia, sku, supervisorL4, usuario);
        await PlanificarLineaAsync(1, turno, dia, sku, supervisorL1, usuario);
        await ConfirmarPlanificacionAsync(turno, dia, usuario);

        // Ambos titulares ausentes; un único Operador B en toda la planta.
        var titularL4 = await CrearPersonaAsync(ctx, "operador_a", situacion: "ausente_justificado");
        var titularL1 = await CrearPersonaAsync(ctx, "operador_a", situacion: "ausente_justificado");
        var operadorB = await CrearPersonaAsync(ctx, "operador_b");
        var puestoL4 = await CrearPuestoAsync(ctx, 4, "fijo", categoriaTitular: "operador_a", titularId: titularL4);
        var puestoL1 = await CrearPuestoAsync(ctx, 1, "fijo", categoriaTitular: "operador_a", titularId: titularL1);

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await ctx.Asignaciones.SingleAsync(a => a.PuestoId == puestoL4)).PersonalId.Should().Be(operadorB);
        (await ctx.Asignaciones.AnyAsync(a => a.PuestoId == puestoL1)).Should().BeFalse("el único Operador B ya lo tomó L4, de mayor prioridad");
        (await FnBitAsync("fn_EsVacanteCritica", ("@p", puestoL1))).Should().BeTrue();
    }

    [Fact]
    public async Task Arrancar_dos_veces_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        await ArrancarTurnoAsync(turno, dia, usuario);

        var codigo = await ArrancarTurnoAsync(turno, dia, usuario);

        codigo.Should().Be("TURNO_YA_ARRANCADO");
    }

    [Fact]
    public async Task Arrancar_sin_confirmar_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var sku = await CrearSkuAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);
        await PlanificarLineaAsync(1, turno, dia, sku, usuario, usuario); // planificada, nunca confirmada

        var codigo = await ArrancarTurnoAsync(turno, dia, usuario);

        codigo.Should().Be("PLANIFICACION_NO_CONFIRMADA");
    }

    [Fact]
    public async Task Arrancar_sin_ninguna_linea_activa_se_rechaza()
    {
        await using var ctx = CrearContexto();
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx, new TimeOnly(6, 0), new TimeOnly(14, 0));
        var dia = new DateOnly(2026, 8, 10);
        await PlanificarLineaAsync(1, turno, dia, skuId: null, supervisorId: null, usuario);
        await ConfirmarPlanificacionAsync(turno, dia, usuario);

        var codigo = await ArrancarTurnoAsync(turno, dia, usuario);

        codigo.Should().Be("SIN_LINEAS_ACTIVAS");
    }

    // ═══ E5.7 — Ventana de arranque por jornada-línea (§8.4, 04 §9) ═══

    [Fact]
    public async Task Arrancar_sin_parametro_configurado_deja_la_ventana_sin_activar()
    {
        // Mismo criterio que fn_VentanaArranqueBloquea (E4.5): sin
        // 'ventana_arranque_min' en Parametro, la regla no aplica todavía
        // — nunca un valor por defecto inventado (R2).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);

        await ArrancarTurnoAsync(turno, dia, usuario);

        var jornada = await ctx.JornadasLinea.SingleAsync();
        jornada.ArrancadoEn.Should().NotBeNull();
        jornada.VentanaArranqueFin.Should().BeNull();
    }

    [Fact]
    public async Task Arrancar_con_parametro_configurado_calcula_la_ventana()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        await SetParametroAsync(ctx, "ventana_arranque_min", "15");
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);

        var antes = DateTime.UtcNow;
        await ArrancarTurnoAsync(turno, dia, usuario);

        var jornada = await ctx.JornadasLinea.SingleAsync();
        jornada.VentanaArranqueFin.Should().NotBeNull();
        (jornada.VentanaArranqueFin!.Value - jornada.ArrancadoEn!.Value).Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(2));
        jornada.ArrancadoEn.Should().BeOnOrAfter(antes.AddSeconds(-2), "hora del servidor, no la del cliente (00 §C6)");
    }

    [Fact]
    public async Task Ventana_activa_bloquea_a_operador_b_que_no_esta_fisicamente_en_la_linea_durante_el_barrido_siguiente()
    {
        // El barrido en sí corre ANTES de que la ventana arranque (02 §3.2:
        // el diagrama pone "arranca la ventana" DESPUÉS del barrido), así
        // que esta prueba verifica el efecto posterior: sp_ValidarAsignacion
        // (E4.5) ya bloquea intentos manuales fuera de la línea una vez la
        // ventana quedó activa tras el arranque.
        await using var ctx = CrearContexto();
        await SetParametroAsync(ctx, "ventana_arranque_min", "15");
        var dia = new DateOnly(2026, 8, 10);
        var (turno, _, usuario) = await PrepararTurnoConfirmadoAsync(ctx, 1, dia);
        var puestoRotativo = await CrearPuestoAsync(ctx, 1, "rotativo");
        var personaDeOtraLinea = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 2);

        await ArrancarTurnoAsync(turno, dia, usuario);

        (await FnBitAsync("fn_VentanaArranqueBloquea", ("@p1", personaDeOtraLinea), ("@p2", puestoRotativo)))
            .Should().BeTrue("§8.4: la ventana ya está activa y la persona no está físicamente en L1");
    }
}
