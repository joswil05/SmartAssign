package com.smartassign.app.ui.pin

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.IdentidadGuardada
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.ui.navegacion.Rutas
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** 04 §6.4: el conteo de intentos vive en el servidor — el cliente solo obedece el código que vuelve. */
class PinViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun pin_correcto_navega_al_destino_resuelto() {
        val repo = FakeSesionRepositorio().apply {
            resultadoPin = ResultadoAuth.Ok(1, "supervisor", "Sup")
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", null)
        }
        val vm = PinViewModel(repo)
        vm.onPinChange("1234")

        var ruta: String? = null
        vm.verificar(alAutenticar = { ruta = it }, alVolverALogin = { })

        assertEquals(Rutas.SIN_LINEA, ruta)
    }

    @Test
    fun pin_incorrecto_muestra_error_y_no_fuerza_login() {
        val repo = FakeSesionRepositorio().apply {
            resultadoPin = ResultadoAuth.Rechazo("PIN_INCORRECTO")
        }
        val vm = PinViewModel(repo)
        vm.onPinChange("0000")

        var volvioALogin = false
        vm.verificar(alAutenticar = {}, alVolverALogin = { volvioALogin = true })

        assertEquals(false, volvioALogin)
        assertTrue(vm.uiState.value.error!!.isNotBlank())
    }

    @Test
    fun tercer_intento_fallido_fuerza_login_completo() {
        // 04 §6.4: el servidor ya cerró la sesión al tercer PIN fallido.
        val repo = FakeSesionRepositorio().apply {
            resultadoPin = ResultadoAuth.Rechazo("PIN_MAX_INTENTOS_SESION_CERRADA")
        }
        val vm = PinViewModel(repo)
        vm.onPinChange("9999")

        var volvioALogin = false
        vm.verificar(alAutenticar = {}, alVolverALogin = { volvioALogin = true })

        assertTrue(volvioALogin)
    }

    @Test
    fun el_pin_solo_acepta_digitos_y_maximo_6() {
        val vm = PinViewModel(FakeSesionRepositorio())
        vm.onPinChange("12ab34")
        assertEquals("", vm.uiState.value.pin)

        vm.onPinChange("1234567")
        assertEquals("", vm.uiState.value.pin)

        vm.onPinChange("123456")
        assertEquals("123456", vm.uiState.value.pin)
    }

    @Test
    fun expone_el_nombre_del_usuario_identificado_localmente() {
        val repo = FakeSesionRepositorio().apply {
            identidadGuardadaValor = IdentidadGuardada(1, "supervisor", "María López")
        }
        val vm = PinViewModel(repo)

        assertEquals("María López", vm.nombreUsuario)
    }
}
