package com.smartassign.app.ui.altadispositivo

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import org.junit.Rule
import org.junit.Test

/**
 * 02 §1.0. La transición a `Escaneando` (cámara real) no se prueba aquí
 * — requiere hardware/permiso de cámara y se cubre en E6.5 (00 §E1).
 * Lo que sí es responsabilidad de esta pantalla, y se prueba de verdad:
 * las instrucciones iniciales y el botón que abre la cámara.
 */
class AltaDispositivoScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun muestra_las_instrucciones_de_cero_tecleo_al_entrar() {
        compose.setContent {
            AltaDispositivoScreen(onServidorConfigurado = {}, viewModel = AltaDispositivoViewModel(FakeSesionRepositorio()))
        }

        compose.onNodeWithTag("alta-dispositivo-instrucciones").assertExists()
        compose.onNodeWithTag("alta-dispositivo-abrir-camara").assertExists()
    }

    @Test
    fun abrir_camara_saca_de_la_pantalla_de_instrucciones() {
        compose.setContent {
            AltaDispositivoScreen(onServidorConfigurado = {}, viewModel = AltaDispositivoViewModel(FakeSesionRepositorio()))
        }

        compose.onNodeWithTag("alta-dispositivo-abrir-camara").performClick()

        compose.onNodeWithTag("alta-dispositivo-instrucciones").assertDoesNotExist()
    }
}
