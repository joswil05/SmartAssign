using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartAssign.Api.Endpoints;
using SmartAssign.Api.Hubs;
using SmartAssign.Api.Notificaciones;
using SmartAssign.Api.Seguridad;
using SmartAssign.Api.TiempoReal;
using SmartAssign.Application.Asignaciones;
using SmartAssign.Application.CicloDeTurno;
using SmartAssign.Application.Operacion;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Historico;
using SmartAssign.Application.Seguridad;
using SmartAssign.Application.Trazabilidad;
using SmartAssign.Application.VersionesApp;
using SmartAssign.Infrastructure.Asignaciones;
using SmartAssign.Infrastructure.CicloDeTurno;
using SmartAssign.Infrastructure.Operacion;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Historico;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Seguridad;
using SmartAssign.Infrastructure.VersionesApp;
using SmartAssign.Infrastructure.Trazabilidad;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ─── Canal en vivo (etapa E12, 05 §2.4) ─────────────────────────────────
builder.Services.AddSignalR();
// Bandeja de salida transaccional (E12.3, 05 §4.1): drena EventoSaliente
// hacia PlantaHub — la garantía transaccional la dio sp_EncolarEvento al
// escribir, este servicio solo entrega lo que ya quedó confirmado.
builder.Services.AddHostedService<EventoSalienteDispatcher>();

// ─── FCM como campana vacía (E12.4, D5/05 §2.5) ─────────────────────────
// Sin credenciales reales de Firebase todavía (mismo hueco que D6/AD) —
// el adaptador real reemplaza este registro cuando el cliente las entregue.
builder.Services.AddSingleton<IServicioNotificacionesPush, ServicioNotificacionesPushSinConfigurar>();
builder.Services.AddHostedService<NotificacionDispatcher>();
// E12.6: escala al Coordinador (AlertaCoordinadorEvento, vía la misma
// bandeja de salida de E12.3) toda notificación crítica sin acuse a
// tiempo — "supervisor no localizable" (D5).
builder.Services.AddHostedService<EscaladoDeNotificacionesDispatcher>();

// ─── Barridos del motor (revisión de producción, P-03) ──────────────────
// sp_DetectarFatiga (E9.1) y sp_CaducarTransitos (E8.6) estaban construidos
// y probados, pero solo los llamaban las pruebas: en planta la fatiga nunca
// se habría detectado sola y ningún tránsito habría caducado.
builder.Services.AddHostedService<BarridosDelMotorService>();

// ─── Identidad y aislamiento (etapa E2) ─────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Seccion));
var jwtOpciones = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración (D6).");

// Revisión de producción, hallazgo P-05. appsettings.json deja la clave
// vacía y afirmaba que "un despliegue que no la fije falla al arrancar".
// No fallaba: el proceso levantaba, escribía "Application started" y
// devolvía 500 a TODOS los endpoints, incluidos los anónimos — el teléfono
// ni siquiera podía verificar el servidor al darse de alta. Ahora falla
// aquí, con el motivo escrito y antes de aceptar una sola petición.
if (string.IsNullOrWhiteSpace(jwtOpciones.ClaveSecreta))
    throw new InvalidOperationException(
        "Jwt:ClaveSecreta está vacía. En producción llega por variable de entorno "
        + "(Jwt__ClaveSecreta) o Key Vault, nunca de un archivo versionado (D6). "
        + "Genera una con: openssl rand -base64 32");

// HMAC-SHA256 con una clave más corta que su propio digest no aporta la
// fuerza que el algoritmo promete, y el handler la rechazaría en la
// primera firma — es decir, en el primer login del turno.
if (Encoding.UTF8.GetByteCount(jwtOpciones.ClaveSecreta) < 32)
    throw new InvalidOperationException(
        $"Jwt:ClaveSecreta mide {Encoding.UTF8.GetByteCount(jwtOpciones.ClaveSecreta)} bytes; "
        + "HMAC-SHA256 necesita al menos 32. Genera una con: openssl rand -base64 32");

builder.Services.AddScoped<IServicioCredenciales, ServicioCredenciales>();
builder.Services.AddScoped<IServicioTokens, ServicioTokensJwt>();
builder.Services.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
builder.Services.AddScoped<IRegistradorAuditoria, RegistradorAuditoria>();
builder.Services.AddScoped<IServicioAsignacion, ServicioAsignacion>();
builder.Services.AddScoped<IServicioHistorico, ServicioHistorico>();
builder.Services.AddScoped<IServicioCicloDeTurno, ServicioCicloDeTurno>();
builder.Services.AddScoped<IServicioOperacion, ServicioOperacion>();
builder.Services.AddScoped<IServicioVersionApp, ServicioVersionApp>();

builder.Services.AddScoped<IContextoSesionActual, ContextoSesionActual>();
builder.Services.AddScoped<IAlcanceLineaResolver, AlcanceLineaResolver>();
builder.Services.AddScoped<SessionContextConnectionInterceptor>();
builder.Services.AddScoped<IAuthorizationHandler, AlcanceLineaHandler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Sin esto, el handler remapea "sub" al URI largo heredado de
        // WS-Federation (compatibilidad histórica) y FindFirst("sub") deja
        // de encontrar el claim: los nombres de ClaimsSmartAssign dejarían
        // de coincidir con lo que de verdad trae el ClaimsPrincipal.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOpciones.Emisor,
            ValidateAudience = true,
            ValidAudience = jwtOpciones.Audiencia,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOpciones.ClaveSecreta)),
            ClockSkew = TimeSpan.FromSeconds(30),
            // Claim "rol" (04 §6.1) mapeado como rol .NET — habilita
            // RequireRole()/IsInRole() sin reinventar la comprobación.
            RoleClaimType = ClaimsSmartAssign.Rol,
            NameClaimType = ClaimsSmartAssign.UsuarioId,
        };
        // SignalR (E12.1): el transporte WebSocket/SSE no puede fijar el
        // header Authorization, así que el cliente manda el access token
        // por querystring — SOLO se acepta esa vía para /hub, nunca para
        // el resto de la Api (04 §6.4: el token sigue viajando por header
        // en cada endpoint REST normal).
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments("/hub"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

// ─── Límite de tasa en credenciales (revisión de producción, P-11) ──────
// El bloqueo por intentos fallidos (E2) es POR USUARIO: frena a quien
// adivina la contraseña de Ana, no a quien prueba una contraseña común
// contra las 160 fichas, ni a un cliente en bucle que tumbe el login al
// arranque del turno. Esto limita por origen, que es la otra mitad.
//
// Solo las rutas anónimas de credenciales. El resto de la Api ya exige
// sesión y va detrás del aislamiento de E2 — limitar ahí penalizaría a un
// supervisor legítimo llenando su línea a toda velocidad, que es
// exactamente lo que el sistema quiere que ocurra.
builder.Services.AddRateLimiter(opciones =>
{
    opciones.AddPolicy("credenciales", contexto =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: contexto.Connection.RemoteIpAddress?.ToString() ?? "sin-origen",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                // Configurable porque en pruebas TODAS las peticiones
                // comparten origen (TestServer no expone IP remota) y un
                // límite pensado para la planta las estrangularía entre sí.
                // El valor de planta es el de por defecto.
                PermitLimit = builder.Configuration.GetValue("Credenciales:IntentosPorMinuto", 10),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    // 429 con Retry-After: un cliente que no sabe cuánto esperar reintenta
    // en bucle y empeora justo lo que el límite intenta contener.
    opciones.OnRejected = async (contexto, ct) =>
    {
        contexto.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out var espera))
            contexto.HttpContext.Response.Headers.RetryAfter =
                ((int)espera.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        await contexto.HttpContext.Response.WriteAsJsonAsync(new
        {
            codigoRechazo = "DEMASIADOS_INTENTOS",
            mensaje = "Demasiados intentos desde este dispositivo. Espera un momento y vuelve a probar.",
        }, ct);
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AlcanceLinea", policy => policy.Requirements.Add(new AlcanceLineaRequirement()));
    // Seguro por defecto: todo endpoint exige sesión salvo que se marque
    // explícitamente AllowAnonymous (solo login/refresh/pin, 04 §6.4).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

// El ORM no decide reglas de negocio (05_TRD.md §1.4): la escritura sobre
// tablas críticas queda denegada a esta cuenta desde la etapa E4
// (04_ESQUEMA_BACKEND.md §7.5). Aquí solo se registra el mapeador — con
// el interceptor que fija SESSION_CONTEXT para la RLS de la capa 3
// (04 §6.3) en cada apertura de conexión física.
builder.Services.AddDbContext<SmartAssignDbContext>((sp, options) =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SmartAssignDb"));
    options.AddInterceptors(sp.GetRequiredService<SessionContextConnectionInterceptor>());
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Revisión de producción, P-10. UseHttpsRedirection incondicional deja
// fuera a los teléfonos cuando el servidor de planta sirve por HTTP en la
// red local: la redirección los manda a un puerto que puede no existir o
// tener un certificado que el dispositivo no confía, y el alta falla sin
// explicación. Se hace explícito y configurable en vez de implícito:
//
//   Https:Redireccion = true   → obligatorio (lo correcto con certificado real)
//   Https:Redireccion = false  → se sirve por HTTP dentro de la red de planta
//
// Por defecto sigue activado: bajarlo tiene que ser una decisión escrita
// en la configuración del despliegue, nunca un descuido.
if (app.Configuration.GetValue("Https:Redireccion", true))
    app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAuthentication();
app.UseMiddleware<ContextoSesionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapAuthEndpoints();
app.MapLineaEndpoints();
app.MapServidorEndpoints();
app.MapPersonalEndpoints();
app.MapAsignacionEndpoints();
app.MapNotificacionEndpoints();
app.MapDispositivoPushEndpoints();
app.MapHistoricoEndpoints();
app.MapAuditoriaEndpoints();
app.MapVersionAppEndpoints();
app.MapCicloDeTurnoEndpoints();
app.MapMaestrosEndpoints();
app.MapOperacionEndpoints();
app.MapHub<PlantaHub>("/hub/planta");

app.Run();

/// <summary>Punto de entrada expuesto para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
