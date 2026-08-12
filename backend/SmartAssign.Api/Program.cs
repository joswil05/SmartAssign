using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SmartAssign.Api.Endpoints;
using SmartAssign.Api.Hubs;
using SmartAssign.Api.Seguridad;
using SmartAssign.Application.Asignaciones;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Application.Seguridad;
using SmartAssign.Application.Trazabilidad;
using SmartAssign.Infrastructure.Asignaciones;
using SmartAssign.Infrastructure.Autenticacion;
using SmartAssign.Infrastructure.Persistence;
using SmartAssign.Infrastructure.Seguridad;
using SmartAssign.Infrastructure.Trazabilidad;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// ─── Canal en vivo (etapa E12, 05 §2.4) ─────────────────────────────────
builder.Services.AddSignalR();

// ─── Identidad y aislamiento (etapa E2) ─────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.Seccion));
var jwtOpciones = builder.Configuration.GetSection(JwtOptions.Seccion).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Falta la sección 'Jwt' en la configuración (D6).");

builder.Services.AddScoped<IServicioCredenciales, ServicioCredenciales>();
builder.Services.AddScoped<IServicioTokens, ServicioTokensJwt>();
builder.Services.AddScoped<IServicioAutenticacion, ServicioAutenticacion>();
builder.Services.AddScoped<IRegistradorAuditoria, RegistradorAuditoria>();
builder.Services.AddScoped<IServicioAsignacion, ServicioAsignacion>();

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

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseMiddleware<ContextoSesionMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapAuthEndpoints();
app.MapLineaEndpoints();
app.MapServidorEndpoints();
app.MapPersonalEndpoints();
app.MapAsignacionEndpoints();
app.MapHub<PlantaHub>("/hub/planta");

app.Run();

/// <summary>Punto de entrada expuesto para WebApplicationFactory en las pruebas de integración.</summary>
public partial class Program;
