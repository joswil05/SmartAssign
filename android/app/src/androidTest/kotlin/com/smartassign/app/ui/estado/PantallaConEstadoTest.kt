package com.smartassign.app.ui.estado

import androidx.compose.foundation.layout.Box
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import org.junit.Rule
import org.junit.Test

/**
 * Prueba real de Compose (con emulador/dispositivo, `connectedDebugAndroidTest`)
 * contra la regla dura de docs/03_UIUX_BRIEF.md §3.11: los cuatro
 * tratamientos nunca se confunden entre sí, y "cargando" nunca se ve como
 * "vacío" ni al revés (§12.4, la distinción más importante del documento).
 */
class PantallaConEstadoTest {

    @get:Rule
    val compose = createComposeRule()

    private val esqueletoDePrueba: @Composable () -> Unit = {
        Box(modifier = Modifier.testTag("esqueleto-de-prueba")) {
            Text("Forma del contenido real")
        }
    }

    @Test
    fun cargando_muestra_el_esqueleto_y_nada_mas() {
        compose.setContent {
            PantallaConEstado(
                estado = EstadoPantalla.Cargando,
                esqueleto = esqueletoDePrueba,
                contenido = { Text("no debería verse") }
            )
        }

        compose.onNodeWithTag("estado-cargando").assertExists()
        compose.onNodeWithTag("esqueleto-de-prueba").assertExists()
        compose.onNodeWithTag("estado-vacio-legitimo").assertDoesNotExist()
        compose.onNodeWithTag("estado-fuera-de-operacion").assertDoesNotExist()
        compose.onNodeWithTag("estado-error").assertDoesNotExist()
    }

    @Test
    fun listo_muestra_el_contenido_real_y_no_el_esqueleto() {
        compose.setContent {
            PantallaConEstado(
                estado = EstadoPantalla.Listo("L4"),
                esqueleto = esqueletoDePrueba,
                contenido = { datos -> Text("Línea $datos") }
            )
        }

        compose.onNodeWithText("Línea L4").assertExists()
        compose.onNodeWithTag("esqueleto-de-prueba").assertDoesNotExist()
        compose.onNodeWithTag("estado-cargando").assertDoesNotExist()
    }

    @Test
    fun vacio_legitimo_muestra_el_mensaje_y_el_siguiente_paso_exactos() {
        compose.setContent {
            PantallaConEstado(
                estado = EstadoPantalla.VacioLegitimo(
                    mensaje = "Ningún puesto en fatiga ahora mismo.",
                    siguientePaso = "Vuelve a revisar en unos minutos."
                ),
                esqueleto = esqueletoDePrueba,
                contenido = { Text("no debería verse") }
            )
        }

        compose.onNodeWithTag("estado-vacio-legitimo").assertExists()
        compose.onNodeWithText("Ningún puesto en fatiga ahora mismo.").assertExists()
        compose.onNodeWithText("Vuelve a revisar en unos minutos.").assertExists()
        // No se confunde con "cargando" — §12.4, la distinción más importante.
        compose.onNodeWithTag("estado-cargando").assertDoesNotExist()
        compose.onNodeWithTag("esqueleto-de-prueba").assertDoesNotExist()
    }

    @Test
    fun fuera_de_operacion_no_se_confunde_con_vacio_ni_con_error() {
        compose.setContent {
            PantallaConEstado(
                estado = EstadoPantalla.FueraDeOperacion(
                    motivo = "No requerido por el SKU de hoy."
                ),
                esqueleto = esqueletoDePrueba,
                contenido = { Text("no debería verse") }
            )
        }

        compose.onNodeWithTag("estado-fuera-de-operacion").assertExists()
        compose.onNodeWithText("No requerido por el SKU de hoy.").assertExists()
        compose.onNodeWithTag("estado-vacio-legitimo").assertDoesNotExist()
        compose.onNodeWithTag("estado-error").assertDoesNotExist()
    }

    @Test
    fun error_muestra_causa_concreta_y_accion_concreta_nunca_un_codigo() {
        compose.setContent {
            PantallaConEstado(
                estado = EstadoPantalla.Error(
                    causa = "No se pudo cargar la línea.",
                    accionSugerida = "Reintentando en 5 s."
                ),
                esqueleto = esqueletoDePrueba,
                contenido = { Text("no debería verse") }
            )
        }

        compose.onNodeWithTag("estado-error").assertExists()
        compose.onNodeWithText("No se pudo cargar la línea.").assertExists()
        compose.onNodeWithText("Reintentando en 5 s.").assertExists()
        // Ningún camino de error debe filtrar un código técnico a pantalla.
        compose.onNodeWithText("HTTP", substring = true).assertDoesNotExist()
        compose.onNodeWithText("Exception", substring = true).assertDoesNotExist()
    }
}
