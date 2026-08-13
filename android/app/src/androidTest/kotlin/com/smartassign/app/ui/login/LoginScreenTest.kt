package com.smartassign.app.ui.login

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.data.version.FakeVerificadorVersionRepositorio
import com.smartassign.app.data.version.ResultadoVersion
import com.smartassign.app.ui.navegacion.Rutas
import org.junit.Rule
import org.junit.Test

/** 02 §1.1: `[Login usuario]` — de verdad en pantalla, con emulador. */
class LoginScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun credenciales_correctas_navega_al_destino_resuelto() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Ok(1, "coordinador", "Coord")
            quienSoyResultado = QuienSoy(1, "coordinador", "Coord", null)
        }
        var rutaRecibida: String? = null

        compose.setContent {
            LoginScreen(onAutenticado = { rutaRecibida = it }, viewModel = LoginViewModel(repo, FakeVerificadorVersionRepositorio()))
        }

        compose.onNodeWithTag("login-usuario").performTextInput("coord_android")
        compose.onNodeWithTag("login-password").performTextInput("Clave#Coord123")
        compose.onNodeWithTag("login-entrar").performClick()

        compose.waitUntil(timeoutMillis = 5_000) { rutaRecibida != null }
        assert(rutaRecibida == Rutas.PANEL_PLANTA)
    }

    @Test
    fun credenciales_incorrectas_muestra_el_mensaje_generico_en_pantalla() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Rechazo("CREDENCIALES_INVALIDAS")
        }

        compose.setContent {
            LoginScreen(onAutenticado = {}, viewModel = LoginViewModel(repo, FakeVerificadorVersionRepositorio()))
        }

        compose.onNodeWithTag("login-usuario").performTextInput("alguien")
        compose.onNodeWithTag("login-password").performTextInput("mal")
        compose.onNodeWithTag("login-entrar").performClick()
        compose.waitForIdle()

        compose.onNodeWithText("Usuario o contraseña incorrectos.").assertExists()
    }

    // ═══ UT-E14.6 (00 §F3): verificación de versión al iniciar sesión ═══

    @Test
    fun bloqueada_por_version_reemplaza_el_formulario_por_completo() {
        val repo = FakeSesionRepositorio()
        val version = FakeVerificadorVersionRepositorio().apply {
            resultado = ResultadoVersion.Bloqueada("2.0.0", "https://servidor/api/version-app/apk")
        }

        compose.setContent {
            LoginScreen(onAutenticado = {}, viewModel = LoginViewModel(repo, version))
        }

        compose.onNodeWithTag("pantalla-login-bloqueada").assertExists()
        compose.onNodeWithTag("pantalla-login").assertDoesNotExist()
        compose.onNodeWithTag("login-usuario").assertDoesNotExist()
        compose.onNodeWithText("2.0.0", substring = true).assertExists()
    }

    @Test
    fun actualizacion_disponible_sin_bloquear_muestra_el_banner_y_el_formulario_sigue_usable() {
        val repo = FakeSesionRepositorio().apply {
            resultadoLogin = ResultadoAuth.Ok(1, "coordinador", "Coord")
            quienSoyResultado = QuienSoy(1, "coordinador", "Coord", null)
        }
        val version = FakeVerificadorVersionRepositorio().apply {
            resultado = ResultadoVersion.ActualizacionDisponible("2.0.0", "https://servidor/api/version-app/apk")
        }
        var rutaRecibida: String? = null

        compose.setContent {
            LoginScreen(onAutenticado = { rutaRecibida = it }, viewModel = LoginViewModel(repo, version))
        }

        compose.onNodeWithTag("login-banner-actualizacion-disponible").assertExists()
        compose.onNodeWithTag("login-usuario").assertExists()

        // "se ofrece la actualización pero no se impone" (00 §F3, literal) — el login sigue funcionando.
        compose.onNodeWithTag("login-usuario").performTextInput("coord_android")
        compose.onNodeWithTag("login-password").performTextInput("Clave#Coord123")
        compose.onNodeWithTag("login-entrar").performClick()

        compose.waitUntil(timeoutMillis = 5_000) { rutaRecibida != null }
        assert(rutaRecibida == Rutas.PANEL_PLANTA)
    }

    @Test
    fun compatible_no_muestra_ni_bloqueo_ni_banner() {
        val repo = FakeSesionRepositorio()
        val version = FakeVerificadorVersionRepositorio().apply { resultado = ResultadoVersion.Compatible }

        compose.setContent {
            LoginScreen(onAutenticado = {}, viewModel = LoginViewModel(repo, version))
        }

        compose.onNodeWithTag("pantalla-login-bloqueada").assertDoesNotExist()
        compose.onNodeWithTag("login-banner-actualizacion-disponible").assertDoesNotExist()
        compose.onNodeWithTag("login-usuario").assertExists()
    }
}
