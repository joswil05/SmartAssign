using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Api.Seguridad;

/// <summary>
/// Resuelve la línea a la que pertenece un recurso <b>desde la base</b> y
/// aplica sobre ella el alcance de E2.
///
/// <b>Por qué no basta con el filtro de ruta.</b>
/// <see cref="AlcanceLineaEndpointFilter"/> sirve cuando la línea viaja en
/// la URL. Aquí no: lo que llega es un puesto, un movimiento o un lote, y
/// de qué línea son lo dice la base, no quien llama. Dejar que el cliente
/// afirme la línea de un recurso sería exactamente el agujero que el
/// aislamiento de tres capas (04 §6.3) existe para cerrar.
///
/// <b>Devuelve 404, no 403, cuando el recurso no aparece.</b> Las tablas de
/// alcance llevan RLS, así que para el supervisor de otra línea el recurso
/// sencillamente no existe. Responder 403 confirmaría que sí existe —
/// filtraría por el código de estado justo lo que la RLS esconde en los
/// datos.
/// </summary>
public static class AlcanceDeRecurso
{
    public static int UsuarioId(this ClaimsPrincipal usuario) =>
        int.Parse(usuario.FindFirstValue(ClaimsSmartAssign.UsuarioId)!);

    /// <summary>La línea del puesto, o null si no está al alcance de quien pregunta.</summary>
    public static Task<byte?> LineaDePuestoAsync(this SmartAssignDbContext db, int puestoId, CancellationToken ct) =>
        db.Puestos.Where(p => p.Id == puestoId).Select(p => (byte?)p.LineaId).SingleOrDefaultAsync(ct);

    /// <summary>
    /// La línea de DESTINO del movimiento: quien recibe o rechaza es el
    /// supervisor al que llega la persona (§10.2), no el que la despachó.
    /// </summary>
    public static Task<byte?> LineaDestinoDeMovimientoAsync(this SmartAssignDbContext db, long movimientoId, CancellationToken ct) =>
        db.Movimientos.Where(m => m.Id == movimientoId).Select(m => (byte?)m.LineaDestino).SingleOrDefaultAsync(ct);

    public static Task<byte?> LineaDeJornadaAsync(this SmartAssignDbContext db, int jornadaLineaId, CancellationToken ct) =>
        db.JornadasLinea.Where(j => j.Id == jornadaLineaId).Select(j => (byte?)j.LineaId).SingleOrDefaultAsync(ct);

    public static Task<byte?> LineaDeLoteAsync(this SmartAssignDbContext db, int loteId, CancellationToken ct) =>
        db.Lotes.Where(l => l.Id == loteId)
            .Select(l => (byte?)l.JornadaLinea.LineaId).SingleOrDefaultAsync(ct);

    /// <summary>
    /// Comprueba el alcance sobre una línea ya resuelta. <c>null</c> como
    /// resultado significa "adelante"; cualquier otra cosa es la respuesta
    /// que hay que devolver tal cual.
    /// </summary>
    public static async Task<IResult?> ComprobarAsync(
        IAuthorizationService autorizador, ClaimsPrincipal usuario, byte? lineaId)
    {
        if (lineaId is null) return Results.NotFound();

        var autorizacion = await autorizador.AuthorizeAsync(usuario, lineaId.Value, "AlcanceLinea");
        return autorizacion.Succeeded ? null : Results.Forbid();
    }
}
