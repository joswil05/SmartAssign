package com.smartassign.app.ui.login

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.ui.navegacion.Rutas
import com.smartassign.app.ui.sesion.MensajesSesion
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Rule
import org.junit.Test

class LoginViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun login_exitoso_navega_al_destino_resuelto_por_quienSoy() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Ok(1, "coordinador", "Coord")
            quienSoyResultado = QuienSoy(1, "coordinador", "Coord", null)
        }
        val vm = LoginViewModel(repo)
        vm.onUsernameChange("coord_android")
        vm.onPasswordChange("Clave#Coord123")

        var rutaRecibida: String? = null
        vm.iniciarSesion { rutaRecibida = it }

        assertEquals(Rutas.PANEL_PLANTA, rutaRecibida)
        assertNull(vm.uiState.value.error)
    }

    @Test
    fun contrasena_equivocada_muestra_un_solo_mensaje_generico_sin_detalle() {
        // 02 §1.1: `⛔ [Error auth]` es uno solo — nunca revela cuál cosa falló.
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Rechazo("CREDENCIALES_INVALIDAS")
        }
        val vm = LoginViewModel(repo)
        vm.onUsernameChange("alguien")
        vm.onPasswordChange("mal")

        var navego = false
        vm.iniciarSesion { navego = true }

        assertEquals(false, navego)
        assertEquals(MensajesSesion.ERROR_LOGIN, vm.uiState.value.error)
    }

    @Test
    fun usuario_bloqueado_muestra_exactamente_el_mismo_mensaje_que_credenciales_invalidas() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Rechazo("USUARIO_BLOQUEADO")
        }
        val vm = LoginViewModel(repo)
        vm.onUsernameChange("alguien")
        vm.onPasswordChange("lo-que-sea")
        vm.iniciarSesion {}

        assertEquals(MensajesSesion.ERROR_LOGIN, vm.uiState.value.error)
    }

    @Test
    fun sin_conexion_muestra_su_propio_mensaje_distinto_del_de_credenciales() {
        val repo = FakeSesionRepositorio().apply { resultadoLogin = ResultadoAuth.SinConexion }
        val vm = LoginViewModel(repo)
        vm.onUsernameChange("alguien")
        vm.onPasswordChange("lo-que-sea")
        vm.iniciarSesion {}

        assertEquals(MensajesSesion.SIN_CONEXION, vm.uiState.value.error)
    }

    @Test
    fun no_envia_con_campos_vacios() {
        val repo = FakeSesionRepositorio()
        val vm = LoginViewModel(repo)

        var navego = false
        vm.iniciarSesion { navego = true }

        assertEquals(false, navego)
    }
}
