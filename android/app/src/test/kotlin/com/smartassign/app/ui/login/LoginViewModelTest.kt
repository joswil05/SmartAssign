package com.smartassign.app.ui.login

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.data.version.FakeVerificadorVersionRepositorio
import com.smartassign.app.data.version.ResultadoVersion
import com.smartassign.app.ui.navegacion.Rutas
import com.smartassign.app.ui.sesion.MensajesSesion
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
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
        val vm = LoginViewModel(repo, FakeVerificadorVersionRepositorio())
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
        val vm = LoginViewModel(repo, FakeVerificadorVersionRepositorio())
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
        val vm = LoginViewModel(repo, FakeVerificadorVersionRepositorio())
        vm.onUsernameChange("alguien")
        vm.onPasswordChange("lo-que-sea")
        vm.iniciarSesion {}

        assertEquals(MensajesSesion.ERROR_LOGIN, vm.uiState.value.error)
    }

    @Test
    fun sin_conexion_muestra_su_propio_mensaje_distinto_del_de_credenciales() {
        val repo = FakeSesionRepositorio().apply { resultadoLogin = ResultadoAuth.SinConexion }
        val vm = LoginViewModel(repo, FakeVerificadorVersionRepositorio())
        vm.onUsernameChange("alguien")
        vm.onPasswordChange("lo-que-sea")
        vm.iniciarSesion {}

        assertEquals(MensajesSesion.SIN_CONEXION, vm.uiState.value.error)
    }

    @Test
    fun no_envia_con_campos_vacios() {
        val repo = FakeSesionRepositorio()
        val vm = LoginViewModel(repo, FakeVerificadorVersionRepositorio())

        var navego = false
        vm.iniciarSesion { navego = true }

        assertEquals(false, navego)
    }

    // ═══ UT-E14.6 (00 §F3): verificación de versión al iniciar sesión ═══

    @Test
    fun al_crear_el_viewmodel_ya_trae_el_resultado_de_version_resuelto() {
        val version = FakeVerificadorVersionRepositorio().apply {
            resultado = ResultadoVersion.ActualizacionDisponible("2.0.0", "https://servidor/api/version-app/apk")
        }
        val vm = LoginViewModel(FakeSesionRepositorio(), version)

        assertEquals(ResultadoVersion.ActualizacionDisponible("2.0.0", "https://servidor/api/version-app/apk"), vm.uiState.value.resultadoVersion)
    }

    @Test
    fun bloqueada_por_version_no_intenta_iniciar_sesion_aunque_las_credenciales_esten_completas() {
        // 00 §F3, literal: "la app solo se bloquea si su código de
        // versión queda por debajo de [version_minima_api]" — segunda
        // capa además de la pantalla bloqueada, defensa en profundidad.
        val repo = FakeSesionRepositorio().apply { resultadoLogin = ResultadoAuth.Ok(1, "coordinador", "Coord") }
        val version = FakeVerificadorVersionRepositorio().apply {
            resultado = ResultadoVersion.Bloqueada("2.0.0", "https://servidor/api/version-app/apk")
        }
        val vm = LoginViewModel(repo, version)
        vm.onUsernameChange("coord_android")
        vm.onPasswordChange("Clave#Coord123")

        var navego = false
        vm.iniciarSesion { navego = true }

        assertEquals(false, navego)
    }

    @Test
    fun compatible_o_sin_dato_del_servidor_no_impide_iniciar_sesion() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Ok(1, "coordinador", "Coord")
            quienSoyResultado = QuienSoy(1, "coordinador", "Coord", null)
        }
        val version = FakeVerificadorVersionRepositorio().apply { resultado = ResultadoVersion.SinDatoDelServidor }
        val vm = LoginViewModel(repo, version)
        vm.onUsernameChange("coord_android")
        vm.onPasswordChange("Clave#Coord123")

        var navego = false
        vm.iniciarSesion { navego = true }

        assertTrue("sin conexión al chequeo de versión no debe bloquear el login (§1.3)", navego)
    }
}
