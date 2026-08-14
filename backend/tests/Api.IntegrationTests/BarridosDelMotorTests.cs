using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartAssign.Api.TiempoReal;
using SmartAssign.Application.Seguridad;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace Api.IntegrationTests;

/// <summary>
/// Revisión de producción, hallazgo <b>P-03</b>: <c>sp_DetectarFatiga</c> y
/// <c>sp_CaducarTransitos</c> estaban construidos y probados, pero nada en
/// producción los llamaba — solo las pruebas. <c>BarridosDelMotorService</c>
/// es quien los ejecuta ahora.
///
/// <b>Lo que de verdad se prueba aquí no es que el SP funcione</b> (eso ya
/// lo cubren <c>SolicitudDeRelevoTests</c> y <c>CaducidadDeTransitoTests</c>
/// desde E8/E9), sino que <b>el servicio de fondo llega a ver las filas</b>.
/// Un scope de fondo no pasa por <c>ContextoSesionMiddleware</c>, así que
/// sin fijar el rol a mano la RLS de <c>Puesto</c>/<c>JornadaLinea</c>
/// (04 §6.3) esconde todo y el barrido informaría de cero fatigados con la
/// planta al límite. Es la trampa que E14.4 ya documentó: el interceptor
/// "cierra en falso" y no hay error, solo silencio.
/// </summary>
public class BarridosDelMotorTests(SmartAssignApiFactory fabrica) : IClassFixture<SmartAssignApiFactory>
{
    private BarridosDelMotorService Barridos()
    {
        // El servicio se registra como IHostedService; se recupera de ahí
        // en vez de construirlo a mano, para probar el que de verdad corre.
        return fabrica.Services.GetServices<IHostedService>().OfType<BarridosDelMotorService>().Single();
    }

    [Fact]
    public async Task El_barrido_de_fatiga_ve_los_puestos_pese_a_la_RLS()
    {
        int puestoId;

        using (var scope = fabrica.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<IContextoSesionActual>().Establecer("coordinador", null);
            var db = scope.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

            var usuario = new Usuario
            {
                Username = $"u_barr_{Guid.NewGuid():N}"[..18], NombreCompleto = "Coordinador",
                Rol = "coordinador", OrigenIdentidad = "local", Activo = true,
            };
            db.Usuarios.Add(usuario);

            var turno = new Turno
            {
                Nombre = $"T_{Guid.NewGuid():N}"[..10],
                HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(14, 0),
            };
            db.Turnos.Add(turno);

            // Umbral de una hora; el ocupante lleva tres. Fatiga real, no simulada.
            var puesto = new Puesto
            {
                LineaId = 3, Codigo = $"B{Guid.NewGuid():N}"[..15],
                NombrePuesto = "Puesto para el barrido", Tipo = "rotativo", HorasEnPuesto = 1,
            };
            db.Puestos.Add(puesto);

            var persona = new Personal
            {
                Ficha = $"F{Guid.NewGuid():N}"[..12],
                NombreCompleto = "Ocupante fatigado", Categoria = "operario",
            };
            db.Personas.Add(persona);
            await db.SaveChangesAsync();
            puestoId = puesto.Id;

            var jornada = new JornadaLinea
            {
                LineaId = 3, TurnoId = turno.Id,
                DiaOperacion = new DateOnly(2026, 1, 2), Estado = "arrancada",
            };
            db.JornadasLinea.Add(jornada);
            await db.SaveChangesAsync();

            db.Asignaciones.Add(new Asignacion
            {
                JornadaLineaId = jornada.Id, PuestoId = puestoId, PersonalId = persona.Id,
                Origen = "manual_supervisor", Inicio = DateTime.UtcNow.AddHours(-3),
                AsignadoPor = usuario.Id,
            });
            await db.SaveChangesAsync();
        }

        var abiertas = await Barridos().DetectarFatigaAsync(CancellationToken.None);

        abiertas.Should().BeGreaterThan(0,
            "el puesto lleva 3 h con un umbral de 1 h — si sale 0, el barrido no está viendo "
            + "las filas y la RLS lo está enmascarando en silencio");

        using var verificacion = fabrica.Services.CreateScope();
        verificacion.ServiceProvider.GetRequiredService<IContextoSesionActual>().Establecer("coordinador", null);
        var lectura = verificacion.ServiceProvider.GetRequiredService<SmartAssignDbContext>();

        var solicitud = await lectura.SolicitudesRelevo
            .Where(s => s.PuestoId == puestoId && s.ResueltaEn == null)
            .SingleOrDefaultAsync();

        solicitud.Should().NotBeNull("§9.4 paso 1: la detección abre la solicitud de relevo");
        solicitud!.Origen.Should().Be("umbral_automatico", "la abrió el barrido, no un supervisor");
    }

    [Fact]
    public async Task El_barrido_de_transitos_corre_sin_error_y_es_idempotente()
    {
        // Sin tránsitos vencidos no hay nada que marcar: lo que se prueba es
        // que el barrido llega hasta el final y no revienta contra la RLS.
        var primera = await Barridos().CaducarTransitosAsync(CancellationToken.None);
        var segunda = await Barridos().CaducarTransitosAsync(CancellationToken.None);

        primera.Should().BeGreaterThanOrEqualTo(0);
        segunda.Should().Be(0, "sp_CaducarTransitos es idempotente por diseño (caducado_en IS NULL)");
    }

    [Fact]
    public void El_servicio_esta_registrado_para_correr_en_produccion()
    {
        // La guarda contra el propio hallazgo: el motor puede estar entero y
        // aun así no ejecutarse nunca si nadie lo engancha al host.
        fabrica.Services.GetServices<IHostedService>()
            .OfType<BarridosDelMotorService>()
            .Should().ContainSingle("P-03 fue exactamente esto: el barrido existía y nadie lo llamaba");
    }
}
