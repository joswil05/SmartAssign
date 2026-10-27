package com.smartassign.app.ui.malla

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * UT-E15.6 (docs/08_AUDITORIA_FRONTEND.md §P1-8): la app tenía dos
 * animaciones infinitas y ninguna consultaba *Quitar animaciones* de los
 * ajustes de accesibilidad de Android.
 *
 * Se prueba la regla, no la animación: [pulsaPorFatigaCritica] es pura, así
 * que la parte que de verdad puede romperse se verifica sin emulador.
 */
class PulsoDeFatigaTest {

    @Test
    fun `solo el nivel critico pulsa, y solo si el sistema permite animaciones`() {
        assertTrue("crítico con animaciones activas debe pulsar", pulsaPorFatigaCritica("critico", true))
        assertFalse("crítico con animaciones desactivadas no debe pulsar", pulsaPorFatigaCritica("critico", false))
        assertFalse("sugerido nunca pulsa", pulsaPorFatigaCritica("sugerido", true))
        assertFalse("normal nunca pulsa", pulsaPorFatigaCritica("normal", true))
    }

    /**
     * La regla de seguridad de la UT. Al desactivar el movimiento, el
     * elemento debe quedar a opacidad **plena**, nunca congelado en el alfa
     * inicial de la animación (0.5f): el pulso es un canal de información
     * (03 §2.1), y a medio alfa el nivel *crítico* se vería más débil que
     * el *sugerido*, invirtiendo la escala. §1.3.
     */
    @Test
    fun `sin animacion el nivel critico se dibuja a opacidad plena, no a media`() {
        assertEquals(
            "ALFA_PLENO debe ser 1f — congelar el pulso a 0.5f haría que 'crítico' " +
                "se viera menos urgente que 'sugerido'",
            1f,
            ALFA_PLENO,
            0.0001f
        )
    }
}
