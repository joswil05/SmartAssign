package com.smartassign.app.ui.arranque

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onAllNodesWithTag
import androidx.compose.ui.test.onNodeWithTag
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.ui.navegacion.Rutas
import org.junit.Rule
import org.junit.Test

/** 02 §1.1: el splash nunca decide en blanco — o navega, o explica por qué no puede. */
class ArranqueScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun sin_servidor_configurado_navega_a_alta_de_dispositivo() {
        val repo = FakeSesionRepositorio().apply { configurado = false }
        var ruta: String? = null

        compose.setContent {
            ArranqueScreen(onNavegarA = { ruta = it }, viewModel = ArranqueViewModel(repo))
        }
        compose.waitUntil(timeoutMillis = 5_000) { ruta != null }

        assert(ruta == Rutas.ALTA_DISPOSITIVO)
    }

    @Test
    fun sin_conexion_con_sesion_guardada_muestra_el_estado_de_error_no_una_pantalla_en_blanco() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.SinConexion
        }

        compose.setContent {
            ArranqueScreen(onNavegarA = {}, viewModel = ArranqueViewModel(repo))
        }
        compose.waitUntil(timeoutMillis = 5_000) {
            compose.onAllNodesWithTag("estado-error").fetchSemanticsNodes(atLeastOneRootRequired = false).isNotEmpty()
        }

        compose.onNodeWithTag("estado-error").assertExists()
    }
}
