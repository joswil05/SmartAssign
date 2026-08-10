package com.smartassign.app.ui.altadispositivo

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** 02 §1.0: cero tecleo — el QR trae la URL, se verifica y solo entonces se guarda. */
class AltaDispositivoViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun empieza_mostrando_las_instrucciones() {
        val vm = AltaDispositivoViewModel(FakeSesionRepositorio())
        assertTrue(vm.estado.value is EstadoAltaDispositivo.Instrucciones)
    }

    @Test
    fun abrir_camara_pasa_a_escaneando() {
        val vm = AltaDispositivoViewModel(FakeSesionRepositorio())
        vm.abrirCamara()
        assertTrue(vm.estado.value is EstadoAltaDispositivo.Escaneando)
    }

    @Test
    fun servidor_que_responde_se_guarda_y_navega() {
        val repo = FakeSesionRepositorio().apply { servidorVerificado = true }
        val vm = AltaDispositivoViewModel(repo)

        var navego = false
        vm.alEscanear("https://planta.smartassign.local", alGuardar = { navego = true })

        assertTrue(navego)
        assertEquals("https://planta.smartassign.local", repo.servidorGuardadoUrl)
    }

    @Test
    fun servidor_que_no_responde_no_guarda_nada_y_muestra_el_texto_exacto_del_mapa() {
        // 02 §1.0: "No se pudo llegar a ese servidor. Revisa que estés en la red de planta."
        val repo = FakeSesionRepositorio().apply { servidorVerificado = false }
        val vm = AltaDispositivoViewModel(repo)

        var navego = false
        vm.alEscanear("https://url-inalcanzable.invalido", alGuardar = { navego = true })

        assertEquals(false, navego)
        assertEquals(null, repo.servidorGuardadoUrl)
        val estado = vm.estado.value
        assertTrue(estado is EstadoAltaDispositivo.NoResponde)
        assertEquals("https://url-inalcanzable.invalido", (estado as EstadoAltaDispositivo.NoResponde).url)
    }

    @Test
    fun reintentar_desde_no_responde_vuelve_a_instrucciones() {
        val repo = FakeSesionRepositorio().apply { servidorVerificado = false }
        val vm = AltaDispositivoViewModel(repo)
        vm.alEscanear("https://url-mala", alGuardar = {})

        vm.reintentar()

        assertTrue(vm.estado.value is EstadoAltaDispositivo.Instrucciones)
    }
}
