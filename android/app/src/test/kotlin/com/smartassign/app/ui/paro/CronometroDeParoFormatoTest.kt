package com.smartassign.app.ui.paro

import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Test

/**
 * `formatearDuracion` es la parte determinista del cronómetro (03 §3.8):
 * el resto (persistencia entre pantallas, tick en vivo) se prueba con
 * Compose real en `androidTest`, pero el formato HH:MM:SS no necesita
 * emulador para verificarse exhaustivamente.
 */
class CronometroDeParoFormatoTest {

    @Test
    fun sin_tiempo_transcurrido_es_00_00_00() {
        val ahora = Instant.parse("2026-08-12T10:00:00Z")
        assertEquals("00:00:00", formatearDuracion(ahora, ahora))
    }

    @Test
    fun el_ejemplo_literal_del_mockup_de_03_3_8() {
        // 03 §3.8: "⏱ PARO · MECÁNICO   00:14:32"
        val inicio = Instant.parse("2026-08-12T10:00:00Z")
        val ahora = inicio.plusSeconds(14 * 60 + 32)
        assertEquals("00:14:32", formatearDuracion(inicio, ahora))
    }

    @Test
    fun pasa_la_hora_sin_perder_el_acarreo() {
        val inicio = Instant.parse("2026-08-12T10:00:00Z")
        val ahora = inicio.plusSeconds(3661) // 1h 01m 01s
        assertEquals("01:01:01", formatearDuracion(inicio, ahora))
    }

    @Test
    fun nunca_muestra_un_tiempo_negativo_si_el_reloj_local_se_adelanta() {
        val inicio = Instant.parse("2026-08-12T10:00:05Z")
        val ahora = Instant.parse("2026-08-12T10:00:00Z") // "antes" que el inicio
        assertEquals("00:00:00", formatearDuracion(inicio, ahora))
    }

    @Test
    fun redondea_hacia_abajo_dentro_del_segundo_en_curso() {
        val inicio = Instant.parse("2026-08-12T10:00:00Z")
        val ahora = inicio.plusMillis(1_900) // 1.9 s → sigue siendo 1 s completo
        assertEquals("00:00:01", formatearDuracion(inicio, ahora))
    }
}
