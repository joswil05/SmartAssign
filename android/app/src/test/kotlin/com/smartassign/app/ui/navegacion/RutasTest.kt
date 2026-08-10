package com.smartassign.app.ui.navegacion

import com.smartassign.app.data.red.LineaVigenteResponse
import org.junit.Assert.assertEquals
import org.junit.Test

/** 02 §1.1, último tramo del mapa — las cuatro ramas, aisladas de red y de Compose. */
class RutasTest {

    @Test
    fun coordinador_siempre_va_al_panel_de_planta_aunque_tenga_una_linea() {
        val conLinea = LineaVigenteResponse(1, "L1", "Línea 1", esBolson = false)
        assertEquals(Rutas.PANEL_PLANTA, destinoTrasAutenticar("coordinador", conLinea))
        assertEquals(Rutas.PANEL_PLANTA, destinoTrasAutenticar("coordinador", null))
    }

    @Test
    fun supervisor_sin_linea_va_al_terminal_sin_linea() {
        assertEquals(Rutas.SIN_LINEA, destinoTrasAutenticar("supervisor", null))
    }

    @Test
    fun supervisor_de_L8_va_al_panel_bolson() {
        val bolson = LineaVigenteResponse(8, "L8", "Bolsón", esBolson = true)
        assertEquals(Rutas.PANEL_BOLSON, destinoTrasAutenticar("supervisor", bolson))
    }

    @Test
    fun supervisor_de_una_linea_normal_va_a_la_malla() {
        val l4 = LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false)
        assertEquals(Rutas.MALLA_LINEA, destinoTrasAutenticar("supervisor", l4))
    }
}
