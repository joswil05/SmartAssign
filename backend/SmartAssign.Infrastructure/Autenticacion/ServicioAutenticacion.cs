using Microsoft.EntityFrameworkCore;
using SmartAssign.Application.Trazabilidad;
using SmartAssign.Application.Autenticacion;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Autenticacion;

/// <summary>
/// Implementación local de D6. El origen "ad" queda declarado en el
/// esquema (04 §6.1) pero sin adaptador propio todavía: no hay un
/// entorno de Active Directory / Entra ID disponible en esta fase de
/// construcción, y su URL, dominio y credenciales de servicio son datos
/// del cliente que nadie ha entregado — inventarlos violaría la cláusula
/// de no invención (07 §2, regla R2). Un usuario con OrigenIdentidad="ad"
/// rechaza el login con un código explícito en vez de fallar en silencio
/// contra credenciales locales inexistentes.
/// </summary>
public class ServicioAutenticacion(
    SmartAssignDbContext db,
    IServicioCredenciales credenciales,
    IServicioTokens tokens,
    IRegistradorAuditoria auditoria) : IServicioAutenticacion
{
    public async Task<ResultadoLogin> IniciarSesionAsync(string username, string password, string deviceId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Username == username, ct);
        if (usuario is null)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.CredencialesInvalidas);

        if (!usuario.Activo)
        {
            await AuditarAsync(usuario, "LOGIN", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.UsuarioInactivo, deviceId, ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.UsuarioInactivo);
        }

        if (usuario.BloqueadoHasta is { } bloqueadoHasta && bloqueadoHasta > DateTime.UtcNow)
        {
            await AuditarAsync(usuario, "LOGIN", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.UsuarioBloqueado, deviceId, ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.UsuarioBloqueado);
        }

        if (usuario.OrigenIdentidad != "local")
        {
            await AuditarAsync(usuario, "LOGIN", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.OrigenAdNoDisponible, deviceId, ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.OrigenAdNoDisponible);
        }

        if (usuario.PasswordHash is null || usuario.PasswordSalt is null ||
            !credenciales.Verificar(password, usuario.PasswordHash, usuario.PasswordSalt))
        {
            usuario.IntentosFallidos++;
            await db.SaveChangesAsync(ct);
            await AuditarAsync(usuario, "LOGIN", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.CredencialesInvalidas, deviceId, ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.CredencialesInvalidas);
        }

        usuario.IntentosFallidos = 0;

        // El teléfono se trata como compartido por línea (D6): al entrar
        // un usuario nuevo en un dispositivo, cierra cualquier sesión que
        // hubiera quedado activa de otro usuario en ese mismo dispositivo.
        var sesionesPrevias = await db.SesionesDispositivo
            .Where(s => s.DeviceId == deviceId && s.RevocadoEn == null)
            .ToListAsync(ct);
        var ahora = DateTime.UtcNow;
        foreach (var previa in sesionesPrevias) previa.RevocadoEn = ahora;

        var access = tokens.GenerarAccessToken(usuario);
        var refreshPlano = tokens.GenerarRefreshTokenOpaco();
        var refreshExpira = ahora + tokens.DuracionRefresh;

        db.SesionesDispositivo.Add(new SesionDispositivo
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuario.Id,
            DeviceId = deviceId,
            RefreshTokenHash = credenciales.HashOpaco(refreshPlano),
            EmitidoEn = ahora,
            ExpiraEn = refreshExpira,
            UltimaActividad = ahora,
        });

        await db.SaveChangesAsync(ct);
        await AuditarAsync(usuario, "LOGIN", ResultadoAuditoria.Ok, null, deviceId, ct);

        return ResultadoLogin.Exito(usuario.Id, usuario.Rol, usuario.NombreCompleto,
            access.Valor, access.ExpiraEn, refreshPlano, refreshExpira);
    }

    public async Task<ResultadoLogin> RenovarAsync(string refreshToken, string deviceId, CancellationToken ct = default)
    {
        var hash = credenciales.HashOpaco(refreshToken);
        var sesion = await db.SesionesDispositivo
            .Include(s => s.Usuario)
            .SingleOrDefaultAsync(s => s.DeviceId == deviceId && s.RevocadoEn == null && s.RefreshTokenHash == hash, ct);

        if (sesion is null)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.RefreshInvalido);

        if (sesion.ExpiraEn <= DateTime.UtcNow)
        {
            sesion.RevocadoEn = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.RefreshExpirado);
        }

        if (!sesion.Usuario.Activo)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.UsuarioInactivo);

        sesion.UltimaActividad = DateTime.UtcNow;
        var access = tokens.GenerarAccessToken(sesion.Usuario);
        await db.SaveChangesAsync(ct);

        return ResultadoLogin.Exito(sesion.Usuario.Id, sesion.Usuario.Rol, sesion.Usuario.NombreCompleto, access.Valor, access.ExpiraEn);
    }

    public async Task<ResultadoLogin> ReentrarConPinAsync(int usuarioId, string pin, string deviceId, CancellationToken ct = default)
    {
        var usuario = await db.Usuarios.SingleOrDefaultAsync(u => u.Id == usuarioId, ct);
        if (usuario is null || !usuario.Activo)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.CredencialesInvalidas);

        if (usuario.PinHash is null || usuario.PinSalt is null)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.PinNoConfigurado);

        var sesionActiva = await db.SesionesDispositivo
            .Where(s => s.UsuarioId == usuarioId && s.DeviceId == deviceId && s.RevocadoEn == null)
            .OrderByDescending(s => s.EmitidoEn)
            .FirstOrDefaultAsync(ct);

        if (!credenciales.Verificar(pin, usuario.PinHash, usuario.PinSalt))
        {
            usuario.IntentosFallidos++;

            // 04 §6.4: "3 PIN fallidos → cierre de sesión y login completo".
            if (usuario.IntentosFallidos >= 3)
            {
                if (sesionActiva is not null) sesionActiva.RevocadoEn = DateTime.UtcNow;
                usuario.IntentosFallidos = 0;
                await db.SaveChangesAsync(ct);
                await AuditarAsync(usuario, "PIN_REENTRADA", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.PinMaxIntentosSesionCerrada, deviceId, ct);
                return ResultadoLogin.Rechazo(CodigosRechazoSesion.PinMaxIntentosSesionCerrada);
            }

            await db.SaveChangesAsync(ct);
            await AuditarAsync(usuario, "PIN_REENTRADA", ResultadoAuditoria.Rechazo, CodigosRechazoSesion.PinIncorrecto, deviceId, ct);
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.PinIncorrecto);
        }

        if (sesionActiva is null)
            return ResultadoLogin.Rechazo(CodigosRechazoSesion.RefreshInvalido);

        usuario.IntentosFallidos = 0;
        sesionActiva.UltimaActividad = DateTime.UtcNow;
        var access = tokens.GenerarAccessToken(usuario);
        await db.SaveChangesAsync(ct);
        await AuditarAsync(usuario, "PIN_REENTRADA", ResultadoAuditoria.Ok, null, deviceId, ct);

        return ResultadoLogin.Exito(usuario.Id, usuario.Rol, usuario.NombreCompleto, access.Valor, access.ExpiraEn);
    }

    public async Task CerrarSesionAsync(int usuarioId, string deviceId, CancellationToken ct = default)
    {
        var ahora = DateTime.UtcNow;

        var sesiones = await db.SesionesDispositivo
            .Where(s => s.UsuarioId == usuarioId && s.DeviceId == deviceId && s.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var s in sesiones) s.RevocadoEn = ahora;

        // 04 §10: el token push se revoca al cerrar sesión, junto con la
        // purga de la caché local cifrada del dispositivo (D3, capa app).
        var tokensPush = await db.DispositivosPush
            .Where(p => p.UsuarioId == usuarioId && p.DeviceId == deviceId && p.RevocadoEn == null)
            .ToListAsync(ct);
        foreach (var p in tokensPush) p.RevocadoEn = ahora;

        await db.SaveChangesAsync(ct);

        var usuario = await db.Usuarios.FindAsync([usuarioId], ct);
        if (usuario is not null)
            await AuditarAsync(usuario, "LOGOUT", ResultadoAuditoria.Ok, null, deviceId, ct);
    }

    private Task AuditarAsync(Usuario usuario, string accion, string resultado, string? codigoRechazo, string deviceId, CancellationToken ct) =>
        auditoria.RegistrarAsync(usuario.Id, usuario.Rol, accion, "Usuario", usuario.Id, usuario.PersonalId, null,
            resultado, codigoRechazo, deviceId, ct);
}
