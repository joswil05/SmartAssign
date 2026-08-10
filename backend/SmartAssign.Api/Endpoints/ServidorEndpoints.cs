namespace SmartAssign.Api.Endpoints;

/// <summary>
/// 05_TRD.md §2.3: "GET /servidor/info — Verificación al escanear el QR de
/// alta: confirma que la URL responde". Anónimo a propósito — se consulta
/// antes de que exista cualquier sesión (02 §1.0), justo después de
/// escanear el QR con la URL del servidor y antes de guardarla.
/// </summary>
public static class ServidorEndpoints
{
    public static IEndpointRouteBuilder MapServidorEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/servidor/info", () => Results.Ok(new { servidor = "SmartAssign" }))
            .AllowAnonymous();

        return app;
    }
}
