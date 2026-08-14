using System.Reflection;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using SmartAssign.Application.CicloDeTurno;
using SmartAssign.Application.Operacion;

namespace Api.IntegrationTests;

/// <summary>
/// Revisión de producción, hallazgo <b>P-02</b>: <b>23 de los 36
/// procedimientos no tenían ninguna vía desde el teléfono</b>. El motor
/// entero estaba construido y probado, y una parte grande sencillamente no
/// se podía invocar.
///
/// Esta prueba es la guarda contra que vuelva a pasar. Lee los
/// procedimientos que de verdad existen en el esquema desplegado y exige
/// que cada uno esté en una de tres listas explícitas: alcanzable por la
/// Api, invocado por otro procedimiento, o herramienta de la CLI. Un
/// procedimiento nuevo que nadie enganche <b>rompe aquí, nombrándose</b>,
/// en vez de quedarse mudo hasta el piloto.
/// </summary>
public class CoberturaDeProcedimientosTests(SmartAssignApiFactory fabrica) : IClassFixture<SmartAssignApiFactory>
{
    /// <summary>
    /// Los llama otro procedimiento, no la Api. Darles endpoint propio
    /// sería exponer un paso interno como si fuera una operación.
    /// </summary>
    private static readonly string[] InvocadosPorOtroProcedimiento =
    [
        "sp_ValidarAsignacion",    // dentro de sp_AsignarPersona (E6.8)
        "sp_BarridoPuestosFijos",  // dentro de sp_ArrancarTurno (E5.7)
        "sp_RegistrarAuditoria",   // desde el registrador, en cada operación
        "sp_EncolarEvento",        // bandeja de salida transaccional (E12.3)
        "sp_EncolarNotificacion",  // desde los procedimientos que notifican
    ];

    /// <summary>
    /// Mantenimiento previo a producción, deliberadamente fuera de la Api:
    /// son operaciones de una sola vez contra la base, no de la aplicación.
    /// </summary>
    private static readonly string[] HerramientasDeLinea =
    [
        "sp_VerificarSinDatosSimulados",
        "sp_PurgarDatosSimulados",
    ];

    /// <summary>
    /// Los ejecuta <c>BarridosDelMotorService</c> por su cuenta (P-03), sin
    /// que nadie los pida desde una pantalla.
    /// </summary>
    private static readonly string[] BarridosDeFondo =
    [
        "sp_DetectarFatiga",
        "sp_CaducarTransitos",
        "sp_EscalarNotificacionesVencidas",
    ];

    [Fact]
    public async Task Todo_procedimiento_del_esquema_tiene_una_via_declarada()
    {
        await using var conexion = new SqlConnection(fabrica.CadenaConexion);
        await conexion.OpenAsync();

        var enElEsquema = (await conexion.QueryAsync<string>("""
            SELECT o.name
              FROM sys.objects o
             WHERE o.type = 'P' AND o.is_ms_shipped = 0 AND o.name LIKE 'sp[_]%'
            """)).ToHashSet();

        enElEsquema.Should().NotBeEmpty("si esto sale vacío, la consulta está mal, no el esquema");

        // Lo que las fachadas de la Api saben invocar, leído de los propios
        // servicios: si alguien añade un método, cuenta solo.
        var alcanzablesPorLaApi = ProcedimientosCitadosPor(
            typeof(IServicioOperacion), typeof(IServicioCicloDeTurno));

        var sinVia = enElEsquema
            .Except(alcanzablesPorLaApi)
            .Except(InvocadosPorOtroProcedimiento)
            .Except(HerramientasDeLinea)
            .Except(BarridosDeFondo)
            .Except(ProcedimientosDeOtrasFachadas)
            .OrderBy(n => n)
            .ToList();

        sinVia.Should().BeEmpty(
            "P-02 fue exactamente esto: procedimientos construidos y probados que nadie podía invocar. "
            + "Si aparece uno nuevo, dale endpoint o decláralo en una de las listas de esta prueba.");
    }

    /// <summary>
    /// Las fachadas anteriores a esta revisión, que ya tenían endpoint desde
    /// su propia etapa.
    /// </summary>
    private static readonly string[] ProcedimientosDeOtrasFachadas =
    [
        "sp_SugerirPuesto", "sp_AsignarPersona",     // E6.7/E6.8
        "sp_CalcularEficiencia",                     // E14.3
        "sp_PublicarVersionApp",                     // E14.6
        "sp_RegistrarParo", "sp_ReanudarProduccion", // E11
    ];

    /// <summary>
    /// Saca los nombres de procedimiento del código YA COMPILADO de las
    /// fachadas: la lista no se mantiene a mano, se deduce de lo que el
    /// código de verdad invoca.
    ///
    /// Escanea el ensamblado entero, tipos generados por el compilador
    /// incluidos. No es un detalle: el cuerpo de un método <c>async</c> no
    /// vive en el método sino en la máquina de estados que el compilador
    /// genera detrás, así que mirar solo los métodos declarados deja fuera
    /// justo a los que hacen el trabajo. Se descubrió aquí, con esta prueba
    /// fallando por un procedimiento que sí estaba invocado.
    /// </summary>
    private static HashSet<string> ProcedimientosCitadosPor(params Type[] contratos)
    {
        var nombres = new HashSet<string>();

        foreach (var ensamblado in contratos.Select(ImplementacionDe).Select(t => t.Assembly).Distinct())
        {
            IEnumerable<Type> tipos;
            try { tipos = ensamblado.GetTypes(); } catch { continue; }

            foreach (var tipo in tipos)
            foreach (var metodo in tipo.GetMethods(
                         BindingFlags.Public | BindingFlags.NonPublic |
                         BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            foreach (var literal in LiteralesDe(metodo))
                if (literal.StartsWith("dbo.sp_", StringComparison.Ordinal))
                    nombres.Add(literal["dbo.".Length..]);
        }

        return nombres;
    }

    private static Type ImplementacionDe(Type contrato)
    {
        var implementacion = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => { try { return a.GetTypes(); } catch { return []; } })
            .FirstOrDefault(t => t is { IsClass: true, IsAbstract: false } && contrato.IsAssignableFrom(t));

        implementacion.Should().NotBeNull($"{contrato.Name} tiene que tener una implementación registrada");
        return implementacion!;
    }

    private static IEnumerable<string> LiteralesDe(MethodInfo metodo)
    {
        byte[]? il;
        try { il = metodo.GetMethodBody()?.GetILAsByteArray(); } catch { yield break; }
        if (il is null) yield break;

        var modulo = metodo.Module;
        for (var i = 0; i < il.Length - 4; i++)
        {
            // 0x72 = ldstr, seguido del token de la cadena.
            if (il[i] != 0x72) continue;

            string? valor = null;
            try { valor = modulo.ResolveString(BitConverter.ToInt32(il, i + 1)); }
            catch { /* el byte 0x72 era parte de otra instrucción, no un ldstr */ }

            if (valor is not null) yield return valor;
        }
    }
}
