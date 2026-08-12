package com.smartassign.app.ui.paro

import androidx.compose.foundation.layout.Column
import androidx.compose.material3.Text
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.setValue
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithContentDescription
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import java.time.Instant
import org.junit.Rule
import org.junit.Test

/**
 * Prueba real de Compose (`connectedDebugAndroidTest`) contra 03 §3.8 y
 * §11.1: la barra existe solo mientras hay un paro abierto, y — la parte
 * que le da nombre a la UT — sigue viéndose cuando la pantalla de abajo
 * cambia, porque vive fuera de lo que la navegación reemplaza.
 */
class CronometroDeParoScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun sin_paro_activo_no_dibuja_nada() {
        compose.setContent {
            CronometroDeParo(paro = null)
        }

        compose.onNodeWithTag("cronometro-de-paro").assertDoesNotExist()
    }

    @Test
    fun con_paro_activo_muestra_categoria_en_mayusculas_y_el_icono() {
        compose.setContent {
            CronometroDeParo(paro = ParoActivo("Mecánico", Instant.now()))
        }

        compose.onNodeWithTag("cronometro-de-paro").assertExists()
        compose.onNodeWithText("PARO · MECÁNICO", substring = true).assertExists()
        compose.onNodeWithContentDescription("Paro en curso").assertExists()
    }

    @Test
    fun al_reanudar_la_produccion_la_barra_desaparece() {
        var paro by mutableStateOf<ParoActivo?>(ParoActivo("Calidad", Instant.now()))

        compose.setContent {
            CronometroDeParo(paro = paro)
        }
        compose.onNodeWithTag("cronometro-de-paro").assertExists()

        paro = null
        compose.waitForIdle()

        compose.onNodeWithTag("cronometro-de-paro").assertDoesNotExist()
    }

    /**
     * §11.1, literal: "aunque el supervisor navegue a otras partes de la
     * aplicación". Se simula la navegación cambiando el contenido de abajo
     * sin recomponer el cronómetro — igual que `GrafoDeNavegacion`, que lo
     * cuelga por fuera del `NavHost` en vez de dentro de un `composable`.
     */
    @Test
    fun sigue_visible_cuando_la_pantalla_de_abajo_cambia_por_otra() {
        var pantallaActual by mutableStateOf("malla")
        val inicio = Instant.now()

        compose.setContent {
            Column {
                CronometroDeParo(paro = ParoActivo("Mecánico", inicio))
                when (pantallaActual) {
                    "malla" -> Text("Pantalla: malla de línea")
                    else -> Text("Pantalla: panel de planta")
                }
            }
        }

        compose.onNodeWithTag("cronometro-de-paro").assertExists()
        compose.onNodeWithText("Pantalla: malla de línea").assertExists()

        pantallaActual = "panel_planta"
        compose.waitForIdle()

        // La pantalla de abajo cambió...
        compose.onNodeWithText("Pantalla: panel de planta").assertExists()
        compose.onNodeWithText("Pantalla: malla de línea").assertDoesNotExist()
        // ...pero el cronómetro, que vive fuera de ese contenido, no se movió.
        compose.onNodeWithTag("cronometro-de-paro").assertExists()
        compose.onNodeWithText("PARO · MECÁNICO", substring = true).assertExists()
    }
}
