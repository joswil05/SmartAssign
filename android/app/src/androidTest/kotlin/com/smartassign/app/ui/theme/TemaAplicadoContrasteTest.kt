package com.smartassign.app.ui.theme

import androidx.compose.material3.LocalContentColor
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.test.junit4.createComposeRule
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow

/**
 * Cierra el hueco exacto por el que se coló un bug real y visible.
 *
 * `DesignTokensTest` (JVM) ya comprobaba que los TOKENS cumplen AAA — y
 * seguía en verde mientras la pantalla era ilegible, porque el problema
 * no estaba en los tokens (`TextPrimary` sobre `BgBase` da 17.59:1) sino
 * en QUÉ ESQUEMA se le entregaba de verdad a Material3 en tiempo de
 * ejecución: con el teléfono en modo claro —el estado de fábrica— se
 * elegía un `lightColorScheme` incompleto y `onBackground` caía al gris
 * casi negro de Material3 (`#1C1B1F`) sobre el fondo oscuro fijo de
 * `themes.xml`: **1.10:1**.
 *
 * Por eso esta prueba es de composición real (`connectedDebugAndroidTest`)
 * y no de tokens: lee `MaterialTheme.colorScheme` DESDE DENTRO de
 * `SmartAssignTheme`, que es lo que de verdad acaba pintando.
 */
class TemaAplicadoContrasteTest {

    @get:Rule
    val compose = createComposeRule()

    private fun canalLineal(c: Float): Float =
        if (c <= 0.03928f) c / 12.92f else ((c + 0.055f) / 1.055f).pow(2.4f)

    private fun luminancia(color: Color): Float =
        0.2126f * canalLineal(color.red) + 0.7152f * canalLineal(color.green) + 0.0722f * canalLineal(color.blue)

    private fun contraste(a: Color, b: Color): Float {
        val (claro, oscuro) = max(luminancia(a), luminancia(b)) to min(luminancia(a), luminancia(b))
        return (claro + 0.05f) / (oscuro + 0.05f)
    }

    /** 03 §5.2: texto normal exige AAA (7:1), por §12.3. */
    private val AAA = 7.0f

    private class ColoresLeidos {
        var fondo = Color.Transparent
        var sobreFondo = Color.Transparent
        var sobreSuperficie = Color.Transparent
        var sobrePrimario = Color.Transparent

        /**
         * El color con el que un `Text` SIN color explícito se pinta de
         * verdad. Es el que de verdad importa: los estilos de `Type.kt`
         * no declaran color, así que todo el texto de la app depende de
         * esto. Leer solo `colorScheme` no basta — durante el arreglo de
         * este bug, `colorScheme.onBackground` ya era correcto mientras
         * la pantalla seguía saliendo negro sobre negro.
         */
        var contenidoLocal = Color.Transparent
    }

    private fun leerColores(): ColoresLeidos {
        val leidos = ColoresLeidos()
        var listo by mutableStateOf(false)
        compose.setContent {
            SmartAssignTheme {
                leidos.fondo = MaterialTheme.colorScheme.background
                leidos.sobreFondo = MaterialTheme.colorScheme.onBackground
                leidos.sobreSuperficie = MaterialTheme.colorScheme.onSurface
                leidos.sobrePrimario = MaterialTheme.colorScheme.onPrimary
                leidos.contenidoLocal = LocalContentColor.current
                listo = true
            }
        }
        compose.waitUntil { listo }
        return leidos
    }

    @Test
    fun el_esquema_que_recibe_material3_es_el_oscuro_de_03_no_el_de_material_por_defecto() {
        val c = leerColores()

        assertEquals("el fondo debe ser el token BgBase de 03 §2", BgBase, c.fondo)
        assertEquals("el texto debe ser el token TextPrimary de 03 §2", TextPrimary, c.sobreFondo)
    }

    /**
     * La prueba que de verdad reproduce el bug. Todo el texto de la app
     * se dibuja con estilos de `Type.kt`, que no declaran color — así que
     * lo que decide si se lee o no es `LocalContentColor`, no el
     * `colorScheme`. Antes del arreglo esto valía NEGRO sobre `#0E1116`.
     */
    @Test
    fun el_color_con_el_que_se_pinta_un_texto_sin_color_propio_cumple_AAA() {
        val c = leerColores()

        val razon = contraste(c.contenidoLocal, c.fondo)
        assertTrue(
            "LocalContentColor sobre el fondo da $razon:1, por debajo de $AAA:1 (03 §5.2 por §12.3). " +
                "Sin un Surface que lo fije, Material3 usa negro y la pantalla queda ilegible.",
            razon >= AAA
        )
        assertEquals("el texto por defecto debe ser el token TextPrimary", TextPrimary, c.contenidoLocal)
    }

    @Test
    fun el_texto_sobre_el_fondo_realmente_aplicado_cumple_AAA() {
        val c = leerColores()

        val contrasteFondo = contraste(c.sobreFondo, c.fondo)
        assertTrue(
            "onBackground sobre background da $contrasteFondo:1, por debajo de $AAA:1 (03 §5.2 / §12.3)",
            contrasteFondo >= AAA
        )

        val contrasteSuperficie = contraste(c.sobreSuperficie, BgSurface)
        assertTrue(
            "onSurface sobre surface da $contrasteSuperficie:1, por debajo de $AAA:1",
            contrasteSuperficie >= AAA
        )
    }

    /**
     * El rótulo de un botón primario lo pinta `onPrimary`, no
     * `onBackground`. Sin declararlo, Material3 usaba el suyo — pensado
     * para su paleta, no para el azul de acción — y salía azul oscuro
     * sobre azul.
     *
     * El umbral aplicable es **4.5:1**, no 7:1, porque `TypeAction` son
     * 24 sp y `03 §5.2` clasifica ≥ 24 sp como texto grande. Esa
     * clasificación no es una excusa retroactiva: el rótulo se subió a
     * 24 sp a propósito (ver `Type.kt`), y el azul se oscureció a la vez
     * hasta el máximo que permite mantener el botón visible sobre el
     * fondo (ver `Color.kt`). Las dos mitades juntas son las que cierran
     * el hueco.
     */
    @Test
    fun el_rotulo_de_un_boton_primario_se_lee_sobre_el_azul_de_accion() {
        val c = leerColores()

        assertEquals("el rótulo debe usar el token de texto claro", TextPrimary, c.sobrePrimario)

        val razon = contraste(c.sobrePrimario, AccionPrimaria)
        assertTrue(
            "onPrimary sobre AccionPrimaria da $razon:1, por debajo de 4.5:1 (03 §5.2, texto ≥ 24 sp)",
            razon >= 4.5f
        )
    }

    /**
     * La otra mitad de la misma regla, y la que impide "arreglar" el
     * contraste del rótulo oscureciendo el azul sin más: `03 §5.2` exige
     * **3:1** a los elementos de interfaz. Un botón que no se distingue
     * del fondo no es más accesible por tener el texto muy legible.
     */
    @Test
    fun el_boton_primario_se_distingue_del_fondo() {
        val c = leerColores()

        val razon = contraste(AccionPrimaria, c.fondo)
        assertTrue(
            "AccionPrimaria sobre el fondo da $razon:1, por debajo de 3:1 (03 §5.2, elementos de interfaz)",
            razon >= 3.0f
        )
    }

    /**
     * Fija por qué el rótulo mide lo que mide. Si alguien lo baja de
     * 24 sp, el umbral que le corresponde vuelve a ser 7:1 —
     * inalcanzable sobre cualquier azul visible— y la app queda otra vez
     * fuera de norma sin que nada más lo delate.
     */
    @Test
    fun el_rotulo_de_boton_se_mantiene_en_el_rango_de_texto_grande() {
        assertTrue(
            "TypeAction mide ${TypeAction.fontSize}; por debajo de 24 sp deja de ser texto grande (03 §5.2)",
            TypeAction.fontSize.value >= 24f
        )
    }

    @Test
    fun el_tema_no_depende_del_modo_del_sistema() {
        // El emulador de esta suite corre en modo CLARO (`Night mode: no`,
        // el estado de fábrica) — que es precisamente la condición bajo la
        // que aparecía el bug. Que aquí salga la paleta oscura es la
        // prueba de que el modo del sistema ya no manda.
        val c = leerColores()

        assertEquals(BgBase, c.fondo)
        assertEquals(TextPrimary, c.sobreFondo)
        assertEquals(TextPrimary, c.sobreSuperficie)
    }
}
