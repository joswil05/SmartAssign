package com.smartassign.app.ui.escaneogafete

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.ui.escaneo.EscaneoQrScreen
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeTitle

/**
 * 00 §E1 — el gafete físico trae el número de ficha impreso junto a su QR.
 * Este componente **solo decodifica**; no confirma identidad ni asienta
 * ningún registro (§12.2 es E6.6, quien envuelve esta pantalla con la
 * tarjeta de confirmación antes de consolidar nada).
 */
@Composable
fun EscaneoGafeteScreen(
    onFichaDecodificada: (String) -> Unit,
    viewModel: EscaneoGafeteViewModel = hiltViewModel()
) {
    val estado by viewModel.estado.collectAsState()

    when (estado) {
        is EstadoEscaneoGafete.Instrucciones -> VistaInstrucciones(onAbrirCamara = viewModel::abrirCamara)

        is EstadoEscaneoGafete.Escaneando -> EscaneoQrScreen(
            instruccion = "Apunta al código del gafete",
            onCodigoDetectado = { ficha -> viewModel.alEscanear(ficha, onFichaDecodificada) }
        )
    }
}

@Composable
private fun VistaInstrucciones(onAbrirCamara: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .testTag("escaneo-gafete-instrucciones")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text("Escanear gafete", style = TypeTitle)
        Text(
            "Apunta la cámara al código QR del gafete",
            style = TypeBody,
            modifier = Modifier.padding(top = Spacing.md)
        )
        Button(
            onClick = onAbrirCamara,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.lg)
                .height(TouchTarget.accionPrimaria)
                .testTag("escaneo-gafete-abrir-camara")
        ) {
            Text("Abrir cámara")
        }
    }
}
