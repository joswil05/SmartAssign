package com.smartassign.app.ui.frescura

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import java.time.Instant
import java.time.temporal.ChronoUnit

/** UT-E13.4 — la lógica pura del sello, sin Compose ni emulador. */
class SelloDeFrescuraLogicaTest {

    private val ahora = Instant.parse("2026-08-12T12:00:00Z")

    @Test
    fun a_dos_minutos_el_texto_es_literal_datos_de_hace_2_min() {
        val cacheadoEn = ahora.minus(2, ChronoUnit.MINUTES)

        assertEquals("Datos de hace 2 min", textoSelloDeFrescura(cacheadoEn, ahora))
    }

    @Test
    fun bajo_el_umbral_el_nivel_es_discreto() {
        val cacheadoEn = ahora.minus(4, ChronoUnit.MINUTES)

        assertEquals(NivelFrescura.Discreto, nivelFrescura(cacheadoEn, ahora))
    }

    @Test
    fun exactamente_en_el_umbral_todavia_es_discreto() {
        // "Bajo antiguedad_maxima" — el umbral en sí no cuenta como
        // superado; solo pasar de largo lo activa.
        val cacheadoEn = ahora.minus(ANTIGUEDAD_MAXIMA_DATOS_MIN, ChronoUnit.MINUTES)

        assertEquals(NivelFrescura.Discreto, nivelFrescura(cacheadoEn, ahora))
    }

    @Test
    fun un_minuto_mas_alla_del_umbral_pasa_a_alerta() {
        val cacheadoEn = ahora.minus(ANTIGUEDAD_MAXIMA_DATOS_MIN + 1, ChronoUnit.MINUTES)

        assertEquals(NivelFrescura.Alerta, nivelFrescura(cacheadoEn, ahora))
    }

    @Test
    fun un_reloj_local_adelantado_nunca_muestra_minutos_negativos() {
        val cacheadoEn = ahora.plus(3, ChronoUnit.MINUTES) // "cacheado en el futuro"

        assertEquals(0L, minutosTranscurridos(cacheadoEn, ahora))
        assertTrue("no debe entrar en alerta por un reloj adelantado", nivelFrescura(cacheadoEn, ahora) == NivelFrescura.Discreto)
    }

    @Test
    fun el_umbral_provisional_es_el_declarado_por_04_9() {
        // 04 §9: antiguedad_maxima_datos_min — "5 (provisional)".
        assertEquals(5L, ANTIGUEDAD_MAXIMA_DATOS_MIN)
    }
}
