package com.smartassign.app.ui.altadispositivo

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.ui.escaneo.EscaneoQrScreen
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeCaption
import com.smartassign.app.ui.theme.TypeTitle

/** 02 §1.0 — el mapa completo: instrucciones → cámara → verificación → guardar o rechazar. */
@Composable
fun AltaDispositivoScreen(
    onServidorConfigurado: () -> Unit,
    viewModel: AltaDispositivoViewModel = hiltViewModel()
) {
    val estado by viewModel.estado.collectAsState()

    when (val actual = estado) {
        is EstadoAltaDispositivo.Instrucciones -> VistaInstrucciones(onAbrirCamara = viewModel::abrirCamara)

        is EstadoAltaDispositivo.Escaneando -> EscaneoQrScreen(
            instruccion = "Apunta al código que te muestra el Coordinador",
            onCodigoDetectado = { url -> viewModel.alEscanear(url, onServidorConfigurado) }
        )

        is EstadoAltaDispositivo.Verificando -> VistaVerificando()

        is EstadoAltaDispositivo.NoResponde -> VistaNoResponde(onReintentar = viewModel::reintentar)
    }
}

@Composable
private fun VistaInstrucciones(onAbrirCamara: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .testTag("alta-dispositivo-instrucciones")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text("Alta de dispositivo", style = TypeTitle)
        Text(
            "Escanea el código que te muestra el Coordinador",
            style = TypeBody,
            modifier = Modifier.padding(top = Spacing.md)
        )
        Button(
            onClick = onAbrirCamara,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.lg)
                .height(TouchTarget.accionPrimaria)
                .testTag("alta-dispositivo-abrir-camara")
        ) {
            Text("Abrir cámara")
        }
    }
}

@Composable
private fun VistaVerificando() {
    Column(
        modifier = Modifier.fillMaxSize().testTag("alta-dispositivo-verificando"),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        CircularProgressIndicator()
    }
}

@Composable
private fun VistaNoResponde(onReintentar: () -> Unit) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .testTag("alta-dispositivo-no-responde")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Texto del propio mapa 02 §1.0.
        Text("No se pudo llegar a ese servidor.", style = TypeBody, color = ColorPeligro)
        Text(
            "Revisa que estés en la red de planta.",
            style = TypeCaption,
            modifier = Modifier.padding(top = Spacing.xs)
        )
        Button(
            onClick = onReintentar,
            modifier = Modifier.padding(top = Spacing.lg).testTag("alta-dispositivo-reintentar")
        ) {
            Text("Reintentar")
        }
    }
}
