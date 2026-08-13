package com.smartassign.app.ui.frescura

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import org.junit.Rule
import org.junit.Test

private const val TEXTO_LITERAL_DE_D5_D12_1 =
    "Pendiente de sincronización — no mover al personal hasta recuperar la red."

/** UT-E13.4 — 00 §D4, §12.1: el banner literal. */
class BannerSinSincronizarScreenTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun visible_false_no_dibuja_nada() {
        compose.setContent {
            BannerSinSincronizar(visible = false)
        }

        compose.onNodeWithTag("banner-sin-sincronizar").assertDoesNotExist()
    }

    @Test
    fun visible_true_muestra_el_texto_literal_de_la_fuente() {
        compose.setContent {
            BannerSinSincronizar(visible = true)
        }

        compose.onNodeWithTag("banner-sin-sincronizar").assertExists()
        compose.onNodeWithText(TEXTO_LITERAL_DE_D5_D12_1).assertExists()
    }
}
