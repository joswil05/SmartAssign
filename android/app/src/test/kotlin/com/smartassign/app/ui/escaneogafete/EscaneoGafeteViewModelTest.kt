package com.smartassign.app.ui.escaneogafete

import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test

/** 00 §E1: el QR del gafete trae solo el número de ficha; §12.1: nada sale hacia un servidor aquí. */
class EscaneoGafeteViewModelTest {

    @Test
    fun empieza_mostrando_las_instrucciones() {
        val vm = EscaneoGafeteViewModel()
        assertTrue(vm.estado.value is EstadoEscaneoGafete.Instrucciones)
    }

    @Test
    fun abrir_camara_pasa_a_escaneando() {
        val vm = EscaneoGafeteViewModel()
        vm.abrirCamara()
        assertTrue(vm.estado.value is EstadoEscaneoGafete.Escaneando)
    }

    @Test
    fun cancelar_desde_escaneando_vuelve_a_instrucciones() {
        val vm = EscaneoGafeteViewModel()
        vm.abrirCamara()
        vm.cancelarEscaneo()
        assertTrue(vm.estado.value is EstadoEscaneoGafete.Instrucciones)
    }

    @Test
    fun al_escanear_entrega_la_ficha_decodificada_tal_cual() {
        val vm = EscaneoGafeteViewModel()
        var fichaRecibida: String? = null

        vm.alEscanear("00417", alDecodificar = { fichaRecibida = it })

        assertEquals("00417", fichaRecibida)
    }

    @Test
    fun al_escanear_recorta_espacios_alrededor_del_valor_crudo() {
        // El QR es texto plano (00 §E1) — no hay garantía de que venga sin
        // espacios si alguien regenera el gafete a mano.
        val vm = EscaneoGafeteViewModel()
        var fichaRecibida: String? = null

        vm.alEscanear("  00417  ", alDecodificar = { fichaRecibida = it })

        assertEquals("00417", fichaRecibida)
    }
}
