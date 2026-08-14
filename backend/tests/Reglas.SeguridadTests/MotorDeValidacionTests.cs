using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Application.Tiempo;
using SmartAssign.Infrastructure.Persistence;

namespace Reglas.SeguridadTests;

/// <summary>
/// UT-E4.1 a E4.8 (docs/PROGRESO.md) — el motor de validación completo:
/// las cinco funciones de §7.1, <c>sp_ValidarAsignacion</c> con el orden
/// exacto, el DENY sobre <c>Asignacion</c>/<c>Auditoria</c>, y la suite de
/// reglas de seguridad médicas por los 8 caminos que exige PC-2.
///
/// Las cinco funciones consultan <c>Puesto</c>, que tiene RLS (04 §6.3,
/// etapa E2) — toda llamada aquí pasa primero por
/// <see cref="ComoCoordinadorAsync"/>, igual que en
/// <c>ImportadorDatosRealesTests</c>: sin ese contexto, las funciones
/// verían cero filas de Puesto y todo resultado sería incorrecto por
/// enmascaramiento, no por la lógica de la regla.
/// </summary>
public class MotorDeValidacionTests : IAsyncLifetime
{
    private readonly string _baseDatos = $"SmartAssignTest_{Guid.NewGuid():N}";
    private string CadenaConexion =>
        $"Server=(localdb)\\MSSQLLocalDB;Database={_baseDatos};Trusted_Connection=True;TrustServerCertificate=True;";

    private SmartAssignDbContext CrearContexto() => new(
        new DbContextOptionsBuilder<SmartAssignDbContext>().UseSqlServer(CadenaConexion).Options);

    private static async Task ComoCoordinadorAsync(SmartAssignDbContext ctx)
    {
        var conexion = ctx.Database.GetDbConnection();
        if (conexion.State != System.Data.ConnectionState.Open) await conexion.OpenAsync();
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

    private static async Task<int> CrearPersonaAsync(SmartAssignDbContext ctx, string categoria,
        string? sexo = null, byte? lineaFisicaActual = null, string situacion = "presente_sin_asignar")
    {
        var p = new Personal
        {
            Ficha = $"F{Guid.NewGuid():N}"[..12],
            NombreCompleto = "Persona de prueba",
            Categoria = categoria,
            Sexo = sexo,
            LineaFisicaActual = lineaFisicaActual,
            Situacion = situacion,
        };
        ctx.Personas.Add(p);
        await ctx.SaveChangesAsync();
        return p.Id;
    }

    private static async Task<int> CrearPuestoAsync(SmartAssignDbContext ctx, string tipo,
        string? categoriaTitular = null, string? sexoPreferente = null,
        short? tipoActividadId = null, short? horasRecuperacion = null, byte lineaId = 1)
    {
        var puesto = new Puesto
        {
            LineaId = lineaId,
            Codigo = $"T{Guid.NewGuid():N}"[..15],
            NombrePuesto = "Puesto de prueba",
            Tipo = tipo,
            CategoriaTitular = categoriaTitular,
            SexoPreferente = sexoPreferente,
            TipoActividadId = tipoActividadId,
            HorasRecuperacion = horasRecuperacion,
        };
        ctx.Puestos.Add(puesto);
        await ctx.SaveChangesAsync();
        return puesto.Id;
    }

    /// <summary>
    /// JornadaLinea exige turno_id/dia_operacion desde la etapa E5 (04
    /// §4.1) — antes de E5 estas pruebas creaban la fila con solo
    /// linea_id. El valor del turno/día es irrelevante para lo que E4
    /// prueba aquí (ventana de arranque, orden del SP); solo hace falta
    /// una fila válida de Turno para satisfacer la FK.
    /// </summary>
    private static async Task<byte> CrearTurnoAsync(SmartAssignDbContext ctx)
    {
        var turno = new Turno { Nombre = $"T_{Guid.NewGuid():N}"[..10], HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0) };
        ctx.Turnos.Add(turno);
        await ctx.SaveChangesAsync();
        return turno.Id;
    }

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

    private static async Task CrearRestriccionAsync(SmartAssignDbContext ctx, int personalId, short capacidadId,
        DateOnly fechaInicio, DateOnly? fechaFin, int registradoPor)
    {
        ctx.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = personalId, CapacidadId = capacidadId,
            FechaInicio = fechaInicio, FechaFin = fechaFin, FechaDictamen = fechaInicio,
            Fuente = "Enfermería", RegistradoPor = registradoPor,
        });
        await ctx.SaveChangesAsync();
    }

    private static async Task VincularCapacidadAsync(SmartAssignDbContext ctx, int puestoId, short capacidadId)
    {
        ctx.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidadId });
        await ctx.SaveChangesAsync();
    }

    // ═══ Helpers de invocación SQL cruda ═══

    private async Task<bool> FnBitAsync(string nombreFuncion, params (string nombre, object? valor)[] parametros)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var contexto = conexion.CreateCommand();
        contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await contexto.ExecuteNonQueryAsync();

        await using var cmd = conexion.CreateCommand();
        var listaParametros = string.Join(", ", parametros.Select(p => p.nombre));
        cmd.CommandText = $"SELECT dbo.{nombreFuncion}({listaParametros})";
        foreach (var (nombre, valor) in parametros)
            cmd.Parameters.AddWithValue(nombre, valor ?? DBNull.Value);

        var resultado = await cmd.ExecuteScalarAsync();
        return resultado switch { bool b => b, byte n => n == 1, _ => false };
    }

    private async Task<(string? Codigo, string? Mensaje)> ValidarAsignacionAsync(
        int personalId, int puestoId, int usuarioId,
        bool permitirCederPerfil = false, bool esLiderazgoManual = false)
    {
        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var contexto = conexion.CreateCommand();
        contexto.CommandText = "EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';";
        await contexto.ExecuteNonQueryAsync();

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "sp_ValidarAsignacion";
        cmd.CommandType = System.Data.CommandType.StoredProcedure;
        cmd.Parameters.AddWithValue("@personal_id", personalId);
        cmd.Parameters.AddWithValue("@puesto_id", puestoId);
        cmd.Parameters.AddWithValue("@usuario_id", usuarioId);
        cmd.Parameters.AddWithValue("@permitir_ceder_perfil", permitirCederPerfil);
        cmd.Parameters.AddWithValue("@es_liderazgo_manual", esLiderazgoManual);
        var pCodigo = new SqlParameter("@codigo_rechazo", System.Data.SqlDbType.VarChar, 40) { Direction = System.Data.ParameterDirection.Output };
        var pMensaje = new SqlParameter("@mensaje", System.Data.SqlDbType.NVarChar, 400) { Direction = System.Data.ParameterDirection.Output };
        cmd.Parameters.Add(pCodigo);
        cmd.Parameters.Add(pMensaje);

        await cmd.ExecuteNonQueryAsync();
        return (pCodigo.Value as string, pMensaje.Value as string);
    }

    // ═══ E4.1 — fn_TieneRestriccionBloqueante (§7.2, C14) ═══

    [Fact]
    public async Task Restriccion_vigente_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), null, usuario);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", DateTime.UtcNow.Date));

        bloquea.Should().BeTrue();
    }

    [Fact]
    public async Task Restriccion_caducada_no_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), new DateOnly(2020, 6, 1), usuario);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", DateTime.UtcNow.Date));

        bloquea.Should().BeFalse("00 §C14: §7.2 evalúa solo las activas hoy");
    }

    [Fact]
    public async Task Restriccion_permanente_bloquea_siempre()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), null, usuario);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", new DateTime(2099, 1, 1)));

        bloquea.Should().BeTrue("00 §C14: fecha_fin NULL = permanente");
    }

    [Fact]
    public async Task Restriccion_que_empieza_manana_no_bloquea_todavia()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        var manana = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1));
        await CrearRestriccionAsync(ctx, persona, 1, manana, null, usuario);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", DateTime.UtcNow.Date));

        bloquea.Should().BeFalse();
    }

    [Fact]
    public async Task Sin_restriccion_registrada_no_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", DateTime.UtcNow.Date));

        bloquea.Should().BeFalse();
    }

    // ═══ E4.2 — fn_CategoriaCompatible (§4.2, casilla por casilla) ═══

    [Theory]
    [InlineData("operador_a", "operador_a", true)]
    [InlineData("operador_a", "operador_b", true)]
    [InlineData("operador_a", "operador_c", false)]
    [InlineData("operador_a", "averiero", false)]
    [InlineData("operador_a", "operario", false)]
    [InlineData("operador_a", "liderazgo", false)]
    public async Task Matriz_puesto_fijo_operador_a(string categoriaTitular, string categoriaPersona, bool esperado)
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoriaPersona);
        var puesto = await CrearPuestoAsync(ctx, "fijo", categoriaTitular: categoriaTitular);

        var compatible = await FnBitAsync("fn_CategoriaCompatible", ("@p1", persona), ("@p2", puesto));

        compatible.Should().Be(esperado);
    }

    [Theory]
    [InlineData("averiero", "averiero", true)]
    [InlineData("averiero", "operador_b", true)]
    [InlineData("averiero", "operador_a", false)]
    [InlineData("averiero", "operador_c", false)]
    [InlineData("averiero", "operario", false)]
    public async Task Matriz_puesto_fijo_averiero(string categoriaTitular, string categoriaPersona, bool esperado)
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoriaPersona);
        var puesto = await CrearPuestoAsync(ctx, "fijo", categoriaTitular: categoriaTitular);

        var compatible = await FnBitAsync("fn_CategoriaCompatible", ("@p1", persona), ("@p2", puesto));

        compatible.Should().Be(esperado);
    }

    [Theory]
    [InlineData("operador_c", "operador_c", true)]
    [InlineData("operador_c", "operador_b", true)]
    [InlineData("operador_c", "operador_a", true)]
    [InlineData("operador_c", "averiero", false)]
    [InlineData("operador_c", "operario", false)]
    public async Task Matriz_puesto_fijo_operador_c(string categoriaTitular, string categoriaPersona, bool esperado)
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoriaPersona);
        var puesto = await CrearPuestoAsync(ctx, "fijo", categoriaTitular: categoriaTitular);

        var compatible = await FnBitAsync("fn_CategoriaCompatible", ("@p1", persona), ("@p2", puesto));

        compatible.Should().Be(esperado);
    }

    [Theory]
    [InlineData("operario", true)]
    [InlineData("operador_b", true)]
    [InlineData("operador_a", false)]
    [InlineData("operador_c", false)]
    [InlineData("averiero", false)]
    [InlineData("liderazgo", false)]
    public async Task Matriz_puesto_rotativo(string categoriaPersona, bool esperado)
    {
        // §4.2: "Los Operadores A y los Averieros no bajan a puestos
        // rotativos" — su habilitación se necesita en su máquina.
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, categoriaPersona);
        var puesto = await CrearPuestoAsync(ctx, "rotativo");

        var compatible = await FnBitAsync("fn_CategoriaCompatible", ("@p1", persona), ("@p2", puesto));

        compatible.Should().Be(esperado);
    }

    [Fact]
    public async Task Puesto_fijo_sin_categoria_titular_no_es_compatible_con_nadie()
    {
        // 00 §G5: categoria_titular queda NULL en los 98 puestos fijos
        // reales hasta que el cliente confirme la categorización. "Nadie
        // compatible todavía" es la lectura segura — nunca "cualquiera".
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operador_a");
        var puesto = await CrearPuestoAsync(ctx, "fijo", categoriaTitular: null);

        var compatible = await FnBitAsync("fn_CategoriaCompatible", ("@p1", persona), ("@p2", puesto));

        compatible.Should().BeFalse();
    }

    // ═══ E4.3 — fn_PerfilIncompatible (§7.3, B12: regla blanda) ═══

    [Fact]
    public async Task Puesto_sin_sexo_preferente_nunca_es_incompatible()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: null);

        var incompatible = await FnBitAsync("fn_PerfilIncompatible", ("@p1", persona), ("@p2", puesto));

        incompatible.Should().BeFalse();
    }

    [Fact]
    public async Task Puesto_indistinto_nunca_es_incompatible()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "femenino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Indistinto");

        var incompatible = await FnBitAsync("fn_PerfilIncompatible", ("@p1", persona), ("@p2", puesto));

        incompatible.Should().BeFalse();
    }

    [Fact]
    public async Task Persona_sin_sexo_registrado_nunca_es_incompatible()
    {
        // §7.3, textual: "Si el dato de la persona no está registrado, la
        // regla no se aplica. Nunca se infiere ni se deduce."
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: null);
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Femenino");

        var incompatible = await FnBitAsync("fn_PerfilIncompatible", ("@p1", persona), ("@p2", puesto));

        incompatible.Should().BeFalse();
    }

    [Fact]
    public async Task Sexo_distinto_al_preferente_es_incompatible()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Femenino");

        var incompatible = await FnBitAsync("fn_PerfilIncompatible", ("@p1", persona), ("@p2", puesto));

        incompatible.Should().BeTrue();
    }

    [Fact]
    public async Task Sexo_igual_al_preferente_es_compatible()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "femenino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Femenino");

        var incompatible = await FnBitAsync("fn_PerfilIncompatible", ("@p1", persona), ("@p2", puesto));

        incompatible.Should().BeFalse();
    }

    // ═══ E4.4 — fn_ViolaNoRepeticion24h (§7.4, A4, A12, B6) ═══

    [Fact]
    public async Task Puesto_sin_horas_de_recuperacion_no_tiene_la_regla()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", horasRecuperacion: null);

        var viola = await FnBitAsync("fn_ViolaNoRepeticion24h", ("@p1", persona), ("@p2", puesto));

        viola.Should().BeFalse("A4/A14: sin dato real de recuperación, la regla no se inventa");
    }

    [Fact]
    public async Task Misma_actividad_dentro_de_la_ventana_de_recuperacion_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var tipoActividad = new TipoActividad { Nombre = "Girar botellas" };
        ctx.TiposActividad.Add(tipoActividad);
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario");
        var puestoAyer = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24);
        var puestoHoy = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24);

        ctx.UltimasTareasJornada.Add(new UltimaTareaJornada
        {
            PersonalId = persona, TipoActividadId = tipoActividad.Id, PuestoId = puestoAyer,
            DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RegistradoEn = DateTime.UtcNow.AddHours(-2), // cerró su turno hace 2h, de las 24 exigidas
        });
        await ctx.SaveChangesAsync();

        var viola = await FnBitAsync("fn_ViolaNoRepeticion24h", ("@p1", persona), ("@p2", puestoHoy));

        viola.Should().BeTrue();
    }

    [Fact]
    public async Task Misma_actividad_tres_jornadas_de_descanso_despues_ya_no_bloquea()
    {
        // 05 §6.2: "regla de 24h con tres días de descanso — sigue
        // bloqueando" se refiere a que NO se limpia por calendario; aquí
        // se prueba el otro lado: una vez pasadas de verdad las horas de
        // recuperación, la regla sí libera (B6 no la vuelve permanente).
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var tipoActividad = new TipoActividad { Nombre = "Girar botellas" };
        ctx.TiposActividad.Add(tipoActividad);
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario");
        var puestoAntes = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24);
        var puestoHoy = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24);

        ctx.UltimasTareasJornada.Add(new UltimaTareaJornada
        {
            PersonalId = persona, TipoActividadId = tipoActividad.Id, PuestoId = puestoAntes,
            DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-3)),
            RegistradoEn = DateTime.UtcNow.AddHours(-72), // 3 días reales, muy por encima de las 24h
        });
        await ctx.SaveChangesAsync();

        var viola = await FnBitAsync("fn_ViolaNoRepeticion24h", ("@p1", persona), ("@p2", puestoHoy));

        viola.Should().BeFalse();
    }

    [Fact]
    public async Task Actividad_distinta_no_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var girarBotellas = new TipoActividad { Nombre = "Girar botellas" };
        var limpieza = new TipoActividad { Nombre = "Limpieza" };
        ctx.TiposActividad.AddRange(girarBotellas, limpieza);
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario");
        var puestoAyer = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: girarBotellas.Id, horasRecuperacion: 24);
        var puestoHoy = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: limpieza.Id, horasRecuperacion: 48);

        ctx.UltimasTareasJornada.Add(new UltimaTareaJornada
        {
            PersonalId = persona, TipoActividadId = girarBotellas.Id, PuestoId = puestoAyer,
            DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
            RegistradoEn = DateTime.UtcNow.AddHours(-1),
        });
        await ctx.SaveChangesAsync();

        var viola = await FnBitAsync("fn_ViolaNoRepeticion24h", ("@p1", persona), ("@p2", puestoHoy));

        viola.Should().BeFalse();
    }

    // ═══ E4.5 — fn_VentanaArranqueBloquea (§8.4) ═══

    [Fact]
    public async Task Sin_jornada_abierta_la_ventana_no_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 2);
        var puesto = await CrearPuestoAsync(ctx, "rotativo", lineaId: 1);

        var bloquea = await FnBitAsync("fn_VentanaArranqueBloquea", ("@p1", persona), ("@p2", puesto));

        bloquea.Should().BeFalse("todavía no existe el barrido (E5.5) que abre jornadas — no hay nada que evaluar");
    }

    [Fact]
    public async Task Ventana_abierta_bloquea_a_quien_no_esta_fisicamente_en_la_linea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx);
        ctx.JornadasLinea.Add(new JornadaLinea
        {
            LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1),
            ArrancadoEn = DateTime.UtcNow.AddMinutes(-5),
            VentanaArranqueFin = DateTime.UtcNow.AddMinutes(10),
        });
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 2); // otra línea
        var puesto = await CrearPuestoAsync(ctx, "rotativo", lineaId: 1);

        var bloquea = await FnBitAsync("fn_VentanaArranqueBloquea", ("@p1", persona), ("@p2", puesto));

        bloquea.Should().BeTrue();
    }

    [Fact]
    public async Task Ventana_abierta_no_bloquea_a_quien_esta_fisicamente_en_la_linea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx);
        ctx.JornadasLinea.Add(new JornadaLinea
        {
            LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1),
            ArrancadoEn = DateTime.UtcNow.AddMinutes(-5),
            VentanaArranqueFin = DateTime.UtcNow.AddMinutes(10),
        });
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 1); // misma línea
        var puesto = await CrearPuestoAsync(ctx, "rotativo", lineaId: 1);

        var bloquea = await FnBitAsync("fn_VentanaArranqueBloquea", ("@p1", persona), ("@p2", puesto));

        bloquea.Should().BeFalse();
    }

    [Fact]
    public async Task Ventana_ya_cerrada_no_bloquea_a_nadie()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var turno = await CrearTurnoAsync(ctx);
        ctx.JornadasLinea.Add(new JornadaLinea
        {
            LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1),
            ArrancadoEn = DateTime.UtcNow.AddMinutes(-30),
            VentanaArranqueFin = DateTime.UtcNow.AddMinutes(-15), // ya pasó
        });
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 2);
        var puesto = await CrearPuestoAsync(ctx, "rotativo", lineaId: 1);

        var bloquea = await FnBitAsync("fn_VentanaArranqueBloquea", ("@p1", persona), ("@p2", puesto));

        bloquea.Should().BeFalse("§8.4: la ventana desbloquea movimientos y desvíos al terminar");
    }

    // ═══ E4.6 — sp_ValidarAsignacion: orden exacto (§7.1, B12) ═══

    [Fact]
    public async Task Asignacion_valida_no_devuelve_codigo_de_rechazo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().BeNull();
    }

    [Fact]
    public async Task Paso1_puesto_ocupado_detiene_antes_que_cualquier_otro_paso()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var titular = await CrearPersonaAsync(ctx, "operario", situacion: "asignado");
        var turno = await CrearTurnoAsync(ctx);
        var jornada = new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1) };
        ctx.JornadasLinea.Add(jornada);
        await ctx.SaveChangesAsync();
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        ctx.Asignaciones.Add(new Asignacion
        {
            JornadaLineaId = jornada.Id, PuestoId = puesto, PersonalId = titular,
            Origen = "manual_supervisor", AsignadoPor = usuario,
        });
        await ctx.SaveChangesAsync();

        // Candidato que ADEMÁS tendría categoría incompatible (liderazgo) —
        // si el orden fuera otro, el rechazo sería CATEGORIA_INCOMPATIBLE.
        var otraPersona = await CrearPersonaAsync(ctx, "liderazgo");

        var (codigo, _) = await ValidarAsignacionAsync(otraPersona, puesto, usuario);

        codigo.Should().Be("PUESTO_OCUPADO");
    }

    [Fact]
    public async Task Paso2_persona_no_disponible_detiene_antes_de_categoria()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "liderazgo", situacion: "en_transito"); // también incompatible en categoría
        var puesto = await CrearPuestoAsync(ctx, "rotativo");

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("PERSONA_NO_DISPONIBLE");
    }

    [Fact]
    public async Task Paso3_categoria_incompatible_detiene_antes_de_medica()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "liderazgo"); // incompatible con rotativo
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), null, usuario); // también bloquearía por médica

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("CATEGORIA_INCOMPATIBLE");
    }

    [Fact]
    public async Task Paso4_medica_detiene_antes_de_perfil_y_nunca_la_salta_ningun_parametro()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Femenino"); // también incompatible en perfil
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), null, usuario);

        // Ni @permitir_ceder_perfil ni @es_liderazgo_manual afectan al paso 4 (00 §B12).
        var (codigoNormal, _) = await ValidarAsignacionAsync(persona, puesto, usuario);
        var (codigoCediendoPerfil, _) = await ValidarAsignacionAsync(persona, puesto, usuario, permitirCederPerfil: true);
        var (codigoLiderazgo, _) = await ValidarAsignacionAsync(persona, puesto, usuario, esLiderazgoManual: true);

        codigoNormal.Should().Be("RESTRICCION_MEDICA");
        codigoCediendoPerfil.Should().Be("RESTRICCION_MEDICA", "ningún parámetro puede saltar el paso 4");
        codigoLiderazgo.Should().Be("RESTRICCION_MEDICA", "A7b: el liderazgo manual salta la categoría, nunca lo médico");
    }

    [Fact]
    public async Task Paso5_perfil_preferente_es_el_unico_que_cede()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario", sexo: "masculino");
        var puesto = await CrearPuestoAsync(ctx, "rotativo", sexoPreferente: "Femenino");

        var (codigoNormal, _) = await ValidarAsignacionAsync(persona, puesto, usuario);
        var (codigoCediendo, _) = await ValidarAsignacionAsync(persona, puesto, usuario, permitirCederPerfil: true);

        codigoNormal.Should().Be("PERFIL_PREFERENTE");
        codigoCediendo.Should().BeNull("00 §B12: el perfil preferente es la única regla que cede");
    }

    [Fact]
    public async Task Paso6_no_repeticion_24h_detiene_antes_de_la_ventana_de_arranque()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var tipoActividad = new TipoActividad { Nombre = "Girar botellas" };
        ctx.TiposActividad.Add(tipoActividad);
        await ctx.SaveChangesAsync();

        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 9); // también fallaría la ventana
        var puestoAyer = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24, lineaId: 1);
        var puestoHoy = await CrearPuestoAsync(ctx, "rotativo", tipoActividadId: tipoActividad.Id, horasRecuperacion: 24, lineaId: 1);
        ctx.UltimasTareasJornada.Add(new UltimaTareaJornada
        {
            PersonalId = persona, TipoActividadId = tipoActividad.Id, PuestoId = puestoAyer,
            DiaOperacion = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), RegistradoEn = DateTime.UtcNow.AddHours(-1),
        });
        var turno = await CrearTurnoAsync(ctx);
        ctx.JornadasLinea.Add(new JornadaLinea
        {
            LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1),
            ArrancadoEn = DateTime.UtcNow.AddMinutes(-2), VentanaArranqueFin = DateTime.UtcNow.AddMinutes(10),
        });
        await ctx.SaveChangesAsync();

        var (codigo, _) = await ValidarAsignacionAsync(persona, puestoHoy, usuario);

        codigo.Should().Be("NO_REPETICION_24H");
    }

    [Fact]
    public async Task Paso7_ventana_de_arranque_es_el_ultimo_paso()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var turno = await CrearTurnoAsync(ctx);
        ctx.JornadasLinea.Add(new JornadaLinea
        {
            LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1),
            ArrancadoEn = DateTime.UtcNow.AddMinutes(-2), VentanaArranqueFin = DateTime.UtcNow.AddMinutes(10),
        });
        await ctx.SaveChangesAsync();
        var persona = await CrearPersonaAsync(ctx, "operario", lineaFisicaActual: 9); // no está en L1
        var puesto = await CrearPuestoAsync(ctx, "rotativo", lineaId: 1);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("VENTANA_ARRANQUE");
    }

    // ═══ E4.7 — DENY sobre tablas críticas (04 §7.5) ═══

    [Fact]
    public async Task DENY_impide_insertar_directo_en_Asignacion_con_la_cuenta_de_aplicacion()
    {
        int personalId, puestoId, jornadaId, usuarioId;
        await using (var ctx = CrearContexto())
        {
            await ComoCoordinadorAsync(ctx);
            usuarioId = await CrearUsuarioAsync(ctx);
            personalId = await CrearPersonaAsync(ctx, "operario");
            puestoId = await CrearPuestoAsync(ctx, "rotativo");
            var turno = await CrearTurnoAsync(ctx);
            var jornada = new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1) };
            ctx.JornadasLinea.Add(jornada);
            await ctx.SaveChangesAsync();
            jornadaId = jornada.Id;
        }

        await using var conexionAdmin = new SqlConnection(CadenaConexion);
        await conexionAdmin.OpenAsync();
        await using (var grant = conexionAdmin.CreateCommand())
        {
            // rol_app necesita SELECT para operar con normalidad — lo que
            // 04 §7.5 le niega es escribir Asignacion directamente,
            // saltándose sp_ValidarAsignacion + sp_AsignarPersona.
            grant.CommandText = "GRANT SELECT ON dbo.Asignacion TO rol_app;";
            await grant.ExecuteNonQueryAsync();
        }

        await using var conexion = new SqlConnection(CadenaConexion);
        await conexion.OpenAsync();
        await using var impersonar = conexion.CreateCommand();
        impersonar.CommandText = "EXECUTE AS USER = 'rol_app';";
        await impersonar.ExecuteNonQueryAsync();

        await using var insertar = conexion.CreateCommand();
        insertar.CommandText = """
            INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por)
            VALUES (@j, @pu, @pe, 'manual_supervisor', @u);
            """;
        insertar.Parameters.AddWithValue("@j", jornadaId);
        insertar.Parameters.AddWithValue("@pu", puestoId);
        insertar.Parameters.AddWithValue("@pe", personalId);
        insertar.Parameters.AddWithValue("@u", usuarioId);

        var accion = async () => await insertar.ExecuteNonQueryAsync();
        var excepcion = await accion.Should().ThrowAsync<SqlException>();
        excepcion.Which.Message.Should().Contain("INSERT permission was denied");

        await using var revertir = conexion.CreateCommand();
        revertir.CommandText = "REVERT;";
        await revertir.ExecuteNonQueryAsync();
    }

    // ═══ E4.8 — Suite de reglas de seguridad médicas × 8 caminos (05 §6.2, PC-2) ═══
    //
    // "Restricciones médicas (§7.2): NUNCA cede. Ningún nivel, ningún
    // motor, ningún rol, ninguna urgencia" (00 §B12). Los 8 caminos:
    // los tres parámetros/roles que alguien podría pensar que la saltan
    // (1-3), el camino de más bajo nivel (4), los tres bordes de vigencia
    // de C14 (5-7), y el único otro camino de escritura que existe: saltarse
    // el SP por completo (8, cubierto también por E4.7).

    private async Task<(int persona, int puesto, int usuario)> SembrarEscenarioMedicoAsync(SmartAssignDbContext ctx)
    {
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), null, usuario);
        return (persona, puesto, usuario);
    }

    [Fact]
    public async Task Camino1_asignacion_normal_bloquea_por_restriccion_medica()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var (persona, puesto, usuario) = await SembrarEscenarioMedicoAsync(ctx);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("RESTRICCION_MEDICA");
    }

    [Fact]
    public async Task Camino2_ceder_perfil_no_libera_la_restriccion_medica()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var (persona, puesto, usuario) = await SembrarEscenarioMedicoAsync(ctx);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario, permitirCederPerfil: true);

        codigo.Should().Be("RESTRICCION_MEDICA");
    }

    [Fact]
    public async Task Camino3_liderazgo_manual_no_libera_la_restriccion_medica()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var (persona, puesto, usuario) = await SembrarEscenarioMedicoAsync(ctx);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario, esLiderazgoManual: true);

        codigo.Should().Be("RESTRICCION_MEDICA", "00 §A7b: la excepción de liderazgo NUNCA salta lo médico");
    }

    [Fact]
    public async Task Camino4_la_funcion_de_bajo_nivel_tambien_confirma_el_bloqueo()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var (persona, puesto, _) = await SembrarEscenarioMedicoAsync(ctx);

        var bloquea = await FnBitAsync("fn_TieneRestriccionBloqueante",
            ("@p1", persona), ("@p2", puesto), ("@p3", DateTime.UtcNow.Date));

        bloquea.Should().BeTrue();
    }

    [Fact]
    public async Task Camino5_restriccion_vigente_hoy_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        // Este "hoy" tiene que ser el MISMO que usa sp_ValidarAsignacion, o
        // la prueba no comprueba lo que dice. Antes ambos eran UTC y
        // coincidían por casualidad; el hallazgo P-01 de la revisión de
        // producción destapó que ese "hoy" compartido estaba mal: con el
        // servidor en UTC−6, de 18:00 a medianoche la fecha UTC ya es la de
        // mañana y un dictamen vigente hoy dejaba de bloquear. El motor pasó
        // a la fecha de planta (00 §C6) y la prueba lo sigue.
        var hoy = FechaPlanta.Hoy();
        await CrearRestriccionAsync(ctx, persona, 1, hoy, hoy, usuario);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("RESTRICCION_MEDICA");
    }

    [Fact]
    public async Task Camino6_restriccion_permanente_bloquea()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var (persona, puesto, usuario) = await SembrarEscenarioMedicoAsync(ctx); // fecha_fin NULL

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().Be("RESTRICCION_MEDICA");
    }

    [Fact]
    public async Task Camino7_restriccion_caducada_no_bloquea_la_asignacion_completa()
    {
        await using var ctx = CrearContexto();
        await ComoCoordinadorAsync(ctx);
        var usuario = await CrearUsuarioAsync(ctx);
        var persona = await CrearPersonaAsync(ctx, "operario");
        var puesto = await CrearPuestoAsync(ctx, "rotativo");
        await VincularCapacidadAsync(ctx, puesto, 1);
        await CrearRestriccionAsync(ctx, persona, 1, new DateOnly(2020, 1, 1), new DateOnly(2020, 6, 1), usuario);

        var (codigo, _) = await ValidarAsignacionAsync(persona, puesto, usuario);

        codigo.Should().BeNull("una restricción cerrada no debe seguir bloqueando — la regla no se vuelve más estricta de lo que C14 pide");
    }

    [Fact]
    public async Task Camino8_insert_directo_saltandose_el_procedimiento_tambien_falla()
    {
        // Mismo mecanismo que E4.7: el único otro "camino" para asignar
        // sin pasar por sp_ValidarAsignacion es escribir Asignacion a
        // mano, y el DENY lo cierra igual de firme que la regla misma.
        await Camino8_verificar();

        async Task Camino8_verificar()
        {
            int personalId, puestoId, jornadaId, usuarioId;
            await using (var ctx = CrearContexto())
            {
                await ComoCoordinadorAsync(ctx);
                var (persona, puesto, usuario) = await SembrarEscenarioMedicoAsync(ctx);
                var turno = await CrearTurnoAsync(ctx);
                var jornada = new JornadaLinea { LineaId = 1, TurnoId = turno, DiaOperacion = new DateOnly(2026, 1, 1) };
                ctx.JornadasLinea.Add(jornada);
                await ctx.SaveChangesAsync();
                personalId = persona; puestoId = puesto; usuarioId = usuario; jornadaId = jornada.Id;
            }

            await using var conexionAdmin = new SqlConnection(CadenaConexion);
            await conexionAdmin.OpenAsync();
            await using (var grant = conexionAdmin.CreateCommand())
            {
                grant.CommandText = "GRANT SELECT ON dbo.Asignacion TO rol_app;";
                await grant.ExecuteNonQueryAsync();
            }

            await using var conexion = new SqlConnection(CadenaConexion);
            await conexion.OpenAsync();
            await using var impersonar = conexion.CreateCommand();
            impersonar.CommandText = "EXECUTE AS USER = 'rol_app';";
            await impersonar.ExecuteNonQueryAsync();

            await using var insertar = conexion.CreateCommand();
            insertar.CommandText = """
                INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id, origen, asignado_por)
                VALUES (@j, @pu, @pe, 'manual_supervisor', @u);
                """;
            insertar.Parameters.AddWithValue("@j", jornadaId);
            insertar.Parameters.AddWithValue("@pu", puestoId);
            insertar.Parameters.AddWithValue("@pe", personalId);
            insertar.Parameters.AddWithValue("@u", usuarioId);

            var accion = async () => await insertar.ExecuteNonQueryAsync();
            await accion.Should().ThrowAsync<SqlException>();

            await using var revertir = conexion.CreateCommand();
            revertir.CommandText = "REVERT;";
            await revertir.ExecuteNonQueryAsync();
        }
    }
}
