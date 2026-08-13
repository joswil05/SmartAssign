package com.smartassign.app.ui.frescura

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import java.time.Instant
import java.time.temporal.ChronoUnit
import org.junit.Rule
import org.junit.Test

/** UT-E13.4 — 00 §D4, 03 §3.7: el sello de frescura como pantalla real. */
class SelloDeFrescuraScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun a_los_dos_minutos_muestra_el_texto_literal() {
        compose.setContent {
            SelloDeFrescura(cacheadoEn = Instant.now().minus(2, ChronoUnit.MINUTES))
        }

        compose.onNodeWithTag("sello-de-frescura").assertExists()
        compose.onNodeWithText("Datos de hace 2 min").assertExists()
    }

    @Test
    fun un_dato_reciente_no_dispara_la_degradacion() {
        compose.setContent {
            ContenidoConDegradacion(cacheadoEn = Instant.now()) {
                androidx.compose.material3.Text("contenido real")
            }
        }

        compose.onNodeWithText("contenido real").assertExists()
        compose.onNodeWithTag("marca-de-agua-sin-sincronizar").assertDoesNotExist()
    }

    @Test
    fun un_dato_mas_viejo_que_el_umbral_muestra_la_marca_de_agua_sin_ocultar_el_contenido() {
        compose.setContent {
            ContenidoConDegradacion(cacheadoEn = Instant.now().minus(ANTIGUEDAD_MAXIMA_DATOS_MIN + 1, ChronoUnit.MINUTES)) {
                androidx.compose.material3.Text("contenido real")
            }
        }

        // §12.1: una terminal sin red se ve igual que una conectada — el
        // contenido real sigue existiendo, nunca se reemplaza.
        compose.onNodeWithText("contenido real").assertExists()
        compose.onNodeWithTag("marca-de-agua-sin-sincronizar").assertExists()
    }
}
