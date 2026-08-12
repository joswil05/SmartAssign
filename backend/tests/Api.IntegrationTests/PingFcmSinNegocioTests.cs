using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using SmartAssign.Api.Notificaciones;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.5 (docs/PROGRESO.md): 05 §2.5, literal — "Hay una prueba
/// automatizada que inspecciona el objeto enviado y falla el build si
/// aparece cualquier campo de negocio." Esta es esa prueba, para el
/// canal de FCM (00 §12.1: "Ningún dato de personal puede salir hacia
/// servicios de terceros") — mismo criterio de guarda que
/// <c>CatalogoDeEventosTests</c> (E12.2) aplicó a <c>AvisoFatigaPlanta</c>/
/// <c>RelevoEnCola</c> para el canal de SignalR.
///
/// Sin base de datos a propósito: lo que se verifica es la FORMA del
/// contrato que de verdad sale hacia FCM — rápida, determinista, corre
/// en cada build — no su entrega en vivo (eso ya lo demuestra E12.4).
/// </summary>
public class PingFcmSinNegocioTests
{
    [Fact]
    public void PingFcm_tiene_exactamente_un_campo_publico_el_id_opaco_de_tipo_texto()
    {
        // La guarda más fuerte posible: si algún día alguien agrega un
        // segundo campo a PingFcm —"para mejorar la notificación", el
        // mismo riesgo que 05 §2.4 describe para AvisoFatigaPlanta— esta
        // prueba falla antes de que ese campo llegue a producción.
        var propiedades = typeof(PingFcm).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        propiedades.Should().HaveCount(1, "el contrato de D5 es UN SOLO id opaco, nunca más");
        propiedades.Single().Name.Should().Be(nameof(PingFcm.E));
        propiedades.Single().PropertyType.Should().Be(typeof(string));
    }

    [Fact]
    public void PingFcm_nunca_lleva_una_propiedad_cuyo_nombre_sugiera_dato_de_negocio()
    {
        string[] palabrasDeNegocio =
        [
            "nombre", "ficha", "linea", "puesto", "categoria", "restriccion",
            "foto", "personal", "titulo", "cuerpo", "payload", "motivo", "usuario",
        ];

        typeof(PingFcm).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Should().NotContain(nombre => palabrasDeNegocio.Any(palabra => nombre.Contains(palabra, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void El_JSON_que_de_verdad_saldria_hacia_FCM_tiene_un_solo_campo()
    {
        // 05 §2.5, ejemplo literal de la fuente: {"data": {"e": "a91f3c"}}.
        // Serializa el objeto REAL —no una copia a mano de lo que se
        // "supone" que trae— para que un atributo de serialización agregado
        // sin querer (p. ej. [JsonInclude] en un campo nuevo) también quede
        // cubierto por esta prueba, no solo un cambio de propiedad pública.
        var ping = new PingFcm("12345");

        using var documento = JsonDocument.Parse(JsonSerializer.Serialize(ping));

        documento.RootElement.EnumerateObject().Should().HaveCount(1);
        documento.RootElement.GetProperty("E").GetString().Should().Be("12345");
    }

    [Fact]
    public void IServicioNotificacionesPush_EnviarAsync_solo_recibe_el_push_token_y_el_ping()
    {
        // Guarda de la FIRMA del único punto de contacto con FCM: si algún
        // día alguien le agrega un parámetro de negocio (p. ej. "nombre"
        // "para loguear mejor"), esta prueba lo atrapa sin depender de que
        // nadie recuerde revisar la interfaz a mano.
        var metodo = typeof(IServicioNotificacionesPush).GetMethod(nameof(IServicioNotificacionesPush.EnviarAsync));

        metodo.Should().NotBeNull();
        metodo!.GetParameters().Select(p => p.Name).Should().BeEquivalentTo(["pushToken", "ping", "ct"]);
        metodo.GetParameters().Single(p => p.Name == "ping").ParameterType.Should().Be(typeof(PingFcm));
    }
}
