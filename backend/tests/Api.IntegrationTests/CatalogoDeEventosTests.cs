using System.Reflection;
using FluentAssertions;
using SmartAssign.Api.TiempoReal;

namespace Api.IntegrationTests;

/// <summary>
/// UT-E12.2 (docs/PROGRESO.md): catálogo de eventos (05_TRD.md §2.4).
/// Pruebas puramente estructurales — sin base de datos, sin `HubConnection`
/// real — porque lo que se verifica es la FORMA del contrato (qué campos
/// trae cada evento, a qué grupo va), no su entrega en vivo (eso es E12.3).
/// </summary>
public class CatalogoDeEventosTests
{
    [Fact]
    public void El_catalogo_cubre_exactamente_los_diez_eventos_de_05_2_4()
    {
        CatalogoEventos.GruposPorEvento.Keys.Should().BeEquivalentTo(
        [
            nameof(Eventos.PuestoActualizadoEvento),
            nameof(Eventos.FatigaAvanzadaEvento),
            nameof(Eventos.AvisoFatigaPlantaEvento),
            nameof(Eventos.RelevoEnColaEvento),
            nameof(Eventos.TransitoEntranteEvento),
            nameof(Eventos.TransitoDemoradoEvento),
            nameof(Eventos.EstadisticaActualizadaEvento),
            nameof(Eventos.ParoIniciadoEvento),
            nameof(Eventos.ParoReanudadoEvento),
            nameof(Eventos.AlertaCoordinadorEvento),
        ]);
    }

    [Theory]
    [InlineData(nameof(Eventos.PuestoActualizadoEvento), new[] { TipoGrupo.LineaDelEvento })]
    [InlineData(nameof(Eventos.FatigaAvanzadaEvento), new[] { TipoGrupo.LineaDelEvento })]
    [InlineData(nameof(Eventos.AvisoFatigaPlantaEvento), new[] { TipoGrupo.Avisos })]
    [InlineData(nameof(Eventos.RelevoEnColaEvento), new[] { TipoGrupo.Bolson })]
    [InlineData(nameof(Eventos.TransitoEntranteEvento), new[] { TipoGrupo.LineaDelEvento })]
    [InlineData(nameof(Eventos.TransitoDemoradoEvento), new[] { TipoGrupo.LineaDelEvento, TipoGrupo.Bolson, TipoGrupo.Planta })]
    [InlineData(nameof(Eventos.EstadisticaActualizadaEvento), new[] { TipoGrupo.LineaDelEvento, TipoGrupo.Planta })]
    [InlineData(nameof(Eventos.ParoIniciadoEvento), new[] { TipoGrupo.LineaDelEvento, TipoGrupo.Planta })]
    [InlineData(nameof(Eventos.ParoReanudadoEvento), new[] { TipoGrupo.LineaDelEvento, TipoGrupo.Planta })]
    [InlineData(nameof(Eventos.AlertaCoordinadorEvento), new[] { TipoGrupo.Planta })]
    public void Cada_evento_va_exactamente_a_los_grupos_de_la_tabla_de_05_2_4(string evento, TipoGrupo[] gruposEsperados)
    {
        CatalogoEventos.GruposPorEvento[evento].Should().BeEquivalentTo(gruposEsperados);
    }

    [Fact]
    public void NombresDeGrupoPara_resuelve_un_evento_de_linea_a_la_cadena_real_del_hub()
    {
        CatalogoEventos.NombresDeGrupoPara(nameof(Eventos.PuestoActualizadoEvento), lineaId: 4)
            .Should().BeEquivalentTo(["linea:4"]);
    }

    [Fact]
    public void NombresDeGrupoPara_resuelve_un_evento_de_varios_grupos_en_una_sola_llamada()
    {
        CatalogoEventos.NombresDeGrupoPara(nameof(Eventos.TransitoDemoradoEvento), lineaId: 6)
            .Should().BeEquivalentTo(["linea:6", NombresDeGrupo.Bolson, NombresDeGrupo.Planta]);
    }

    [Fact]
    public void NombresDeGrupoPara_no_necesita_linea_para_un_evento_que_no_la_usa()
    {
        CatalogoEventos.NombresDeGrupoPara(nameof(Eventos.AlertaCoordinadorEvento))
            .Should().BeEquivalentTo([NombresDeGrupo.Planta]);
    }

    [Fact]
    public void NombresDeGrupoPara_exige_la_linea_cuando_el_evento_la_necesita()
    {
        var accion = () => CatalogoEventos.NombresDeGrupoPara(nameof(Eventos.PuestoActualizadoEvento));

        accion.Should().Throw<ArgumentNullException>("mandar el evento sin línea a ningún sitio sería un error silencioso");
    }

    [Fact]
    public void Un_evento_fuera_del_catalogo_se_rechaza_explicitamente()
    {
        var accion = () => CatalogoEventos.NombresDeGrupoPara("EventoQueNoExiste");

        accion.Should().Throw<ArgumentOutOfRangeException>();
    }

    // ═══ Guardas de identidad (00 §D1, §D2) — "hay una prueba automatizada que lo verifica" ═══

    private static readonly string[] PalabrasDeIdentidad =
        ["nombre", "ficha", "foto", "personalid", "restriccion"];

    private static IEnumerable<string> PropiedadesQueParecenIdentidad(Type tipo) =>
        tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .Where(nombre => PalabrasDeIdentidad.Any(palabra => nombre.Contains(palabra, StringComparison.OrdinalIgnoreCase)));

    [Fact]
    public void AvisoFatigaPlanta_no_lleva_ningun_campo_de_identidad()
    {
        // 05 §2.4, literal: "si algún día alguien añade el nombre del operario a
        // ese evento, viola §2.2 para nueve supervisores a la vez. Hay una
        // prueba automatizada que lo verifica" — esta es esa prueba.
        PropiedadesQueParecenIdentidad(typeof(Eventos.AvisoFatigaPlantaEvento)).Should().BeEmpty();

        // Y más estricto todavía: el único campo permitido es el texto ya compuesto.
        typeof(Eventos.AvisoFatigaPlantaEvento).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).Should().BeEquivalentTo([nameof(Eventos.AvisoFatigaPlantaEvento.Texto)]);
    }

    [Fact]
    public void RelevoEnCola_no_lleva_ningun_campo_de_identidad_de_personal_ajeno()
    {
        // 00 §D1, literal: "nombre, ficha ni foto del operario... ningún otro
        // dato del personal de otra línea" — mismo criterio de guarda que
        // AvisoFatigaPlanta, aplicado al otro evento con la misma exigencia.
        PropiedadesQueParecenIdentidad(typeof(Eventos.RelevoEnColaEvento)).Should().BeEmpty();
    }

    [Fact]
    public void TransitoEntrante_si_lleva_nombre_a_proposito_es_el_contraste_deliberado()
    {
        // 05 §2.4, literal: "Nombre — el destino sí lo ve, va a recibirla".
        typeof(Eventos.TransitoEntranteEvento).GetProperty(nameof(Eventos.TransitoEntranteEvento.NombreCompleto))
            .Should().NotBeNull();
    }
}
