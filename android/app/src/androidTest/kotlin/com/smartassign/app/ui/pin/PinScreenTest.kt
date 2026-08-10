package com.smartassign.app.ui.pin

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.compose.ui.test.performTextInput
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.ResultadoAuth
import org.junit.Rule
import org.junit.Test

class PinScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun tres_intentos_fallidos_regresa_a_login_de_verdad_en_pantalla() {
        val repo = FakeSesionRepositorio().apply {
            resultadoPin = ResultadoAuth.Rechazo("PIN_MAX_INTENTOS_SESION_CERRADA")
        }
        var volvioALogin = false

        compose.setContent {
            PinScreen(onAutenticado = {}, onVolverALogin = { volvioALogin = true }, viewModel = PinViewModel(repo))
        }

        compose.onNodeWithTag("pin-campo").performTextInput("0000")
        compose.onNodeWithTag("pin-verificar").performClick()
        compose.waitForIdle()

        assert(volvioALogin)
    }

    @Test
    fun pin_incorrecto_no_fuerza_login_y_muestra_error() {
        val repo = FakeSesionRepositorio().apply { resultadoPin = ResultadoAuth.Rechazo("PIN_INCORRECTO") }
        var volvioALogin = false

        compose.setContent {
            PinScreen(onAutenticado = {}, onVolverALogin = { volvioALogin = true }, viewModel = PinViewModel(repo))
        }

        compose.onNodeWithTag("pin-campo").performTextInput("1111")
        compose.onNodeWithTag("pin-verificar").performClick()
        compose.waitForIdle()

        assert(!volvioALogin)
        compose.onNodeWithTag("pin-error").assertExists()
    }
}
