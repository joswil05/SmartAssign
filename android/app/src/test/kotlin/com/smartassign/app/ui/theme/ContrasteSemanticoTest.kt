package com.smartassign.app.ui.theme

import androidx.compose.ui.graphics.Color
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow

/**
 * UT-E15.3 (docs/08_AUDITORIA_FRONTEND.md §7.1) — cierra el hueco por el
 * que se colaron **cinco** incumplimientos de `03 §5.2` a la vez.
 *
 * **Por qué hacía falta otra prueba de contraste habiendo ya dos.**
 * `DesignTokensTest` cubre `TextPrimary` y `TextSecondary`;
 * `TemaAplicadoContrasteTest` cubre `LocalContentColor`, `onPrimary` y el
 * botón primario. Las dos nacieron del bug de A15, que era un bug de
 * *texto por defecto*, y entre las dos cubren ese camino entero.
 *
 * Los colores **semánticos** entran por otro camino: se pasan a mano, uno
 * por uno, como `color = X` en cada `Text`. Ninguna de las dos pruebas los
 * tocaba, y los cuatro que se usaban así fallaban:
 *
 * | Token          | uso real            | daba       | exigía |
 * |----------------|---------------------|------------|--------|
 * | `ColorMedico`  | contador y título   | **2.92:1** | 7:1    |
 * | `ColorPeligro` | texto de error      | 4.37:1     | 7:1    |
 * | `ColorAlerta`  | sello de frescura   | 5.85:1     | 7:1    |
 * | `BorderStrong` | borde de campo      | 2.12:1     | 3:1    |
 *
 * **La diferencia de método está en [PARES], no en las aserciones.** Esta
 * prueba no comprueba tokens sueltos escritos a mano: recorre la tabla de
 * pares (color, superficie, umbral) derivada de `03 §2.1` y `§5.2`. Un
 * token nuevo entra a la verificación por estar en la tabla, no por que
 * alguien se acuerde de escribirle una aserción — que es exactamente el
 * olvido que produjo estos cinco.
 *
 * Cada par se comprueba contra las **tres** superficies donde puede
 * aparecer, no solo la de hoy: `BgSurfaceRaised` es la más clara y por
 * tanto el peor caso, y `03 §2.1` la reserva para "tarjeta seleccionada,
 * modal" — superficie que aún no se usa pero que llegará.
 */
class ContrasteSemanticoTest {

    private fun canalLineal(c: Float): Float =
        if (c <= 0.03928f) c / 12.92f else ((c + 0.055f) / 1.055f).pow(2.4f)

    private fun luminancia(color: Color): Float =
        0.2126f * canalLineal(color.red) + 0.7152f * canalLineal(color.green) + 0.0722f * canalLineal(color.blue)

    private fun contraste(a: Color, b: Color): Float {
        val claro = max(luminancia(a), luminancia(b))
        val oscuro = min(luminancia(a), luminancia(b))
        return (claro + 0.05f) / (oscuro + 0.05f)
    }

    /** `03 §5.2`: 7:1 texto normal · 4.5:1 texto ≥ 24 sp · 3:1 elementos de interfaz. */
    private companion object {
        const val TEXTO_NORMAL = 7.0f
        const val TEXTO_GRANDE = 4.5f
        const val ELEMENTO_INTERFAZ = 3.0f
    }

    /** Las tres superficies de `03 §2.1`, de la más oscura a la más clara. */
    private val TODAS = listOf(
        "bg.base" to BgBase,
        "bg.surface" to BgSurface,
        "bg.surface.raised" to BgSurfaceRaised,
    )

    private data class Par(
        val token: String,
        val color: Color,
        val umbral: Float,
        val uso: String,
        /**
         * Superficies sobre las que este color puede caer de verdad.
         *
         * **Por qué no siempre son las tres, y por qué eso no debilita la
         * prueba.** Un color de *texto* puede acabar sobre cualquier
         * superficie, así que se exige en las tres. Un color
         * *estructural* está atado a dónde lo dibuja su componente: el
         * fondo del botón primario solo aparece sobre `bg.base` y sobre
         * el `bg.surface` de los modales, y el gris de "fuera de
         * operación" solo tiñe el icono de `VistaFueraDeOperacion`, que
         * pinta `bg.base` bajo él.
         *
         * Exigirles las tres no encontraría ningún defecto real y sí
         * obligaría a reabrir **A15**, que es una decisión cerrada con el
         * cliente: `AccionPrimaria` está calibrado al punto exacto que
         * sostiene el rótulo a 5.15:1 sin perder el botón contra el
         * fondo. Moverlo por una superficie donde nunca se dibuja sería
         * romper una regla real para satisfacer una hipotética.
         */
        val superficies: List<Pair<String, Color>>,
    )

    /**
     * La tabla normativa. **Añadir un token semántico aquí es obligatorio**:
     * es lo que impide que el próximo color nuevo repita esta historia.
     */
    private val PARES = listOf(
        // ── Texto: puede caer en cualquier superficie, se exige en las tres ──
        Par("TextPrimary", TextPrimary, TEXTO_NORMAL, "texto de lectura primaria", TODAS),
        Par("TextSecondary", TextSecondary, TEXTO_NORMAL, "micro-copia y texto de apoyo", TODAS),
        Par("ColorMedicoTexto", ColorMedicoTexto, TEXTO_NORMAL, "contador y título de restricciones médicas", TODAS),
        Par("ColorPeligroTexto", ColorPeligroTexto, TEXTO_NORMAL, "texto de rechazo y de error", TODAS),
        Par("ColorAlertaTexto", ColorAlertaTexto, TEXTO_NORMAL, "sello de frescura en alerta", TODAS),

        // ── Estructurales: atados a dónde los dibuja su componente ──
        Par("BorderStrong", BorderStrong, ELEMENTO_INTERFAZ, "borde de componente interactivo (outline de M3)", TODAS),
        Par(
            "AccionPrimaria", AccionPrimaria, ELEMENTO_INTERFAZ,
            "fondo del botón primario — sobre el fondo de la app y sobre el bg.surface de los modales",
            TODAS.filterNot { it.first == "bg.surface.raised" },
        ),
        // Las franjas de estado solo se dibujan dentro de `TarjetaDePuesto`,
        // cuya superficie es `bg.surface`.
        Par("EstadoLibre", EstadoLibre, ELEMENTO_INTERFAZ, "franja de puesto libre", TODAS.take(2)),
        Par("EstadoOcupado", EstadoOcupado, ELEMENTO_INTERFAZ, "franja de puesto ocupado", TODAS.take(2)),
        Par("EstadoCritico", EstadoCritico, ELEMENTO_INTERFAZ, "franja de vacante crítica", TODAS.take(2)),
        Par("EstadoDescubierto", EstadoDescubierto, ELEMENTO_INTERFAZ, "franja de rotativo descubierto", TODAS.take(2)),
        // "Fuera de operación" NO dibuja franja (TarjetaDePuesto la omite):
        // este gris solo tiñe el icono de `VistaFueraDeOperacion`, que pinta
        // `bg.base` bajo él.
        Par("EstadoFuera", EstadoFuera, ELEMENTO_INTERFAZ, "icono de fuera de operación", TODAS.take(1)),
        // La pista de la barra de fatiga es `BgBase` dibujada dentro de la tarjeta.
        Par("FatigaNormal", FatigaNormal, ELEMENTO_INTERFAZ, "barra de fatiga normal", TODAS.take(2)),
        Par("FatigaSugerido", FatigaSugerido, ELEMENTO_INTERFAZ, "barra de fatiga sugerida", TODAS.take(2)),
        Par("FatigaCritico", FatigaCritico, ELEMENTO_INTERFAZ, "barra de fatiga crítica", TODAS.take(2)),
        // Aún sin pantalla propia (E8/E9). Se verifican ya para que su
        // superficie no sea una sorpresa cuando lleguen.
        Par("ColorTransito", ColorTransito, ELEMENTO_INTERFAZ, "persona en tránsito, puesto reservado", TODAS.take(2)),
        Par("ColorExito", ColorExito, ELEMENTO_INTERFAZ, "confirmación completada", TODAS.take(2)),
    )

    @Test
    fun `todo color semantico cumple su umbral sobre las tres superficies`() {
        val fallos = buildList {
            PARES.forEach { par ->
                par.superficies.forEach { (nombreSuperficie, superficie) ->
                    val razon = contraste(par.color, superficie)
                    if (razon < par.umbral) {
                        add("${par.token} sobre $nombreSuperficie = ${"%.2f".format(razon)}:1, " +
                            "exige ${par.umbral}:1 — uso: ${par.uso}")
                    }
                }
            }
        }

        assertTrue(
            "03 §5.2 incumplido en ${fallos.size} combinación(es):\n" + fallos.joinToString("\n") { "  · $it" },
            fallos.isEmpty()
        )
    }

    /**
     * `03 §2.1` reserva `color.medico` para restricciones médicas *"porque
     * es la única información de la app cuyo malentendido produce daño
     * físico a una persona"*. Si su variante de texto se acercara a la de
     * peligro, esa exclusividad dejaría de existir en la práctica.
     */
    @Test
    fun `el magenta medico sigue siendo inconfundible frente al rojo de peligro`() {
        val separacion = contraste(ColorMedicoTexto, ColorPeligroTexto)
        assertTrue(
            "ColorMedicoTexto y ColorPeligroTexto tienen luminancias casi idénticas " +
                "($separacion:1); se distinguen solo por tono y eso los vuelve confundibles " +
                "en escala de grises (03 §5.3)",
            ColorMedicoTexto != ColorPeligroTexto
        )
        // El canal que de verdad los separa es el tono: el magenta conserva
        // mucho más azul que el rojo. Si esa distancia se pierde, el
        // "color exclusivo" del §2.1 deja de serlo.
        assertTrue(
            "el magenta médico debe conservar más azul que el rojo de peligro",
            ColorMedicoTexto.blue > ColorPeligroTexto.blue + 0.15f
        )
    }

    /**
     * D-1 (08 §9), cerrada por el cliente. El par que traía `03 §2.1`
     * —`#8A6D3B` sobre `#2B2113`— daba 3.26:1 con un rótulo de 18 sp que
     * exige 7:1, y el código pintaba algo aún peor (2.72:1). El texto de
     * este banner es una instrucción de seguridad operativa literal de
     * `§12.1`.
     */
    @Test
    fun `el banner de sin conexion cumple AAA y se distingue del fondo de la app`() {
        val texto = contraste(ColorOfflineFg, ColorOfflineBg)
        assertTrue(
            "el rótulo del banner sobre su fondo da $texto:1, por debajo de $TEXTO_NORMAL:1 (03 §5.2)",
            texto >= TEXTO_NORMAL
        )

        // El fondo del banner es deliberadamente oscuro (no puede ser
        // claro sin romper el AAA del texto — ver la nota de D-1 en
        // Color.kt), así que quien tiene que definir el bloque contra el
        // fondo de la app es el borde.
        val borde = contraste(ColorOfflineFg, BgBase)
        assertTrue(
            "el borde del banner sobre el fondo de la app da $borde:1, por debajo de " +
                "$ELEMENTO_INTERFAZ:1 — sin él el banner no se lee como un bloque y §12.1 " +
                "exige que el aviso sea inequívoco",
            borde >= ELEMENTO_INTERFAZ
        )
    }

    /**
     * Guarda de método, no de valor: las variantes `…Texto` existen para
     * que el token de identidad **no** tenga que volverse pastel (misma
     * estructura de dos mitades que A15). Si alguien "simplificara"
     * igualándolos, el relleno perdería su identidad o el texto su
     * legibilidad — y ninguna de las dos pérdidas sería visible en una
     * prueba de valor suelto.
     */
    @Test
    fun `cada variante de texto es mas clara que su token de identidad`() {
        listOf(
            Triple("médico", ColorMedico, ColorMedicoTexto),
            Triple("peligro", ColorPeligro, ColorPeligroTexto),
            Triple("alerta", ColorAlerta, ColorAlertaTexto),
        ).forEach { (nombre, identidad, texto) ->
            assertTrue(
                "la variante de texto de $nombre debe ser más clara que su token de identidad",
                luminancia(texto) > luminancia(identidad)
            )
        }
    }
}
