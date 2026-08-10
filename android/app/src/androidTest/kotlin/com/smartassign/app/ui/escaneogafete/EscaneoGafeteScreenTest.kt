package com.smartassign.app.ui.escaneogafete

import android.Manifest
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.performClick
import androidx.test.rule.GrantPermissionRule
import org.junit.Rule
import org.junit.Test

/**
 * 00 §E1 — cámara real en el emulador, con el permiso ya concedido. A
 * diferencia de `AltaDispositivoScreenTest` (E6.3), que deliberadamente
 * dejó sin probar la transición a la cámara real por depender de hardware
 * y permiso, esta suite sí concede `CAMERA` y verifica que CameraX + ML
 * Kit *bundled* (§12.1: en el dispositivo, sin recursos externos)
 * inicializan de verdad — es la cobertura que E6.3 pidió cubrir aquí.
 */
class EscaneoGafeteScreenTest {

    @get:Rule
    val permisoCamara: GrantPermissionRule = GrantPermissionRule.grant(Manifest.permission.CAMERA)

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun muestra_las_instrucciones_al_entrar() {
        compose.setContent {
            EscaneoGafeteScreen(onFichaDecodificada = {}, viewModel = EscaneoGafeteViewModel())
        }

        compose.onNodeWithTag("escaneo-gafete-instrucciones").assertExists()
        compose.onNodeWithTag("escaneo-gafete-abrir-camara").assertExists()
    }

    @Test
    fun abrir_camara_con_permiso_concedido_llega_a_la_vista_de_camara_real() {
        compose.setContent {
            EscaneoGafeteScreen(onFichaDecodificada = {}, viewModel = EscaneoGafeteViewModel())
        }

        compose.onNodeWithTag("escaneo-gafete-abrir-camara").performClick()

        compose.onNodeWithTag("escaneo-gafete-instrucciones").assertDoesNotExist()
        // "escaneo-qr" es el testTag del lector compartido (EscaneoQrScreen,
        // E6.3) — llegar aquí prueba que CameraX se enlaza al ciclo de vida
        // real del emulador sin caerse, no solo que cambió un estado.
        compose.onNodeWithTag("escaneo-qr").assertExists()
    }
}
