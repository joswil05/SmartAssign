package com.smartassign.app.ui.theme

import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.sp
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.max
import kotlin.math.min
import kotlin.math.pow

/**
 * Verifica contra el propio texto de docs/03_UIUX_BRIEF.md §2 — no contra
 * una suposición de diseño. Cada prueba cita la regla exacta que cubre.
 *
 * Pruebas de JVM puras (`testDebugUnitTest`): sin emulador, corren en CI
 * igual que las de backend. `Color`/`TextStyle` de Compose UI no dependen
 * del runtime de Android para construirse ni leerse.
 */
class DesignTokensTest {

    // --- Contraste AAA (7:1) para texto "normal" — §2.1: "el contraste
    // mínimo es AAA (7:1) para texto normal", validado aquí y no solo
    // afirmado en el comentario de Color.kt.

    private fun canalLineal(c: Float): Float =
        if (c <= 0.03928f) c / 12.92f else ((c + 0.055f) / 1.055f).pow(2.4f)

    private fun luminanciaRelativa(color: Color): Float =
        0.2126f * canalLineal(color.red) + 0.7152f * canalLineal(color.green) + 0.0722f * canalLineal(color.blue)

    private fun razonDeContraste(a: Color, b: Color): Float {
        val la = luminanciaRelativa(a)
        val lb = luminanciaRelativa(b)
        val claro = max(la, lb)
        val oscuro = min(la, lb)
        return (claro + 0.05f) / (oscuro + 0.05f)
    }

    private val AAA = 7.0f

    @Test
    fun `texto primario cumple AAA sobre las tres superficies de fondo`() {
        assertTrue("primary/bg.base", razonDeContraste(TextPrimary, BgBase) >= AAA)
        assertTrue("primary/bg.surface", razonDeContraste(TextPrimary, BgSurface) >= AAA)
        assertTrue("primary/bg.surface.raised", razonDeContraste(TextPrimary, BgSurfaceRaised) >= AAA)
    }

    @Test
    fun `texto secundario cumple AAA sobre bg base y bg surface`() {
        assertTrue("secondary/bg.base", razonDeContraste(TextSecondary, BgBase) >= AAA)
        assertTrue("secondary/bg.surface", razonDeContraste(TextSecondary, BgSurface) >= AAA)
    }

    // --- Distinción visual — §2.1: "Cinco estados que nunca pueden
    // confundirse entre sí" / "Tres niveles [de fatiga]". El color es un
    // canal más (principio 4, nunca el único), pero dentro de cada familia
    // no puede haber dos tokens con el mismo valor.

    @Test
    fun `los 5 colores de estado de puesto son unicos entre si`() {
        val estados = listOf(EstadoLibre, EstadoOcupado, EstadoCritico, EstadoDescubierto, EstadoFuera)
        assertEquals("no debe haber dos estados de puesto con el mismo color", estados.size, estados.distinct().size)
    }

    @Test
    fun `los 3 niveles de fatiga son unicos entre si`() {
        val niveles = listOf(FatigaNormal, FatigaSugerido, FatigaCritico)
        assertEquals("no debe haber dos niveles de fatiga con el mismo color", niveles.size, niveles.distinct().size)
    }

    // --- Piso tipográfico — §2.2: "No se define ningún tamaño por debajo
    // de 15 sp en toda la aplicación."

    @Test
    fun `ningun estilo tipografico baja de 15 sp`() {
        TodosLosEstilosTipograficos.forEach { estilo ->
            assertTrue("${estilo.fontSize} < 15sp", estilo.fontSize >= 15.sp)
        }
    }

    // --- Zonas de toque — 00_DECISIONES.md §A11: "48 dp — mínimo estándar
    // de Android" y "separación mínima entre dos zonas de toque: 8 dp".

    @Test
    fun `piso de zona de toque es 48dp y la separacion minima es 8dp, segun A11`() {
        assertEquals(48, TouchTarget.minimo.value.toInt())
        assertEquals(8, Spacing.touch.value.toInt())
    }
}
