namespace SmartAssign.Domain.Entities;

/// <summary>
/// D5 / 04 §10 — el lado de CONTENIDO COMPLETO de "FCM como campana
/// vacía". Un evento de negocio inserta aquí, dentro de su misma
/// transacción (vía <c>sp_EncolarNotificacion</c>) — si esa transacción
/// revierte, la fila nunca existió; si confirma, queda lista para que
/// <c>NotificacionDispatcher</c> (Api) le mande a cada dispositivo activo
/// del destinatario un ping SIN nada de esto (<c>PingFcm</c>, un solo id
/// opaco). El contenido real — <see cref="Titulo"/>, <see cref="Cuerpo"/>,
/// <see cref="PayloadJson"/> — nunca viaja hacia Google: se descarga por
/// HTTPS con JWT, <c>GET /notificaciones/{id}</c>, después de que el
/// ping despierte la app.
///
/// <see cref="EntregadaEn"/>/<see cref="AcusadaEn"/>/<see cref="EscaladaEn"/>
/// son la capa de garantía del "sí o sí": ninguna app puede garantizar
/// entrega al 100 %, pero el servidor sí puede garantizar que nadie crea
/// que llegó cuando no llegó (§1.3 aplicado a la infraestructura).
/// <see cref="AcusadaEn"/>/<see cref="EscaladaEn"/> quedan sin escribir
/// todavía — esa capa es E12.6, no esta UT.
/// </summary>
public class Notificacion
{
    public long Id { get; set; }
    public int UsuarioId { get; set; }
    public string Tipo { get; set; } = default!;

    /// <summary>"normal" | "critica" (CK_Notif_criticidad, 04 §10).</summary>
    public string Criticidad { get; set; } = "normal";

    public string Titulo { get; set; } = default!;
    public string Cuerpo { get; set; } = default!;
    public string? PayloadJson { get; set; }

    public DateTime CreadaEn { get; set; }
    public DateTime? EntregadaEn { get; set; }
    public DateTime? AcusadaEn { get; set; }
    public DateTime? EscaladaEn { get; set; }

    public Usuario Usuario { get; set; } = default!;
}
