package com.smartassign.app.ui.escaneo

import android.Manifest
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ExperimentalGetImage
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.viewinterop.AndroidView
import androidx.core.content.ContextCompat
import androidx.lifecycle.compose.LocalLifecycleOwner
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.common.InputImage
import com.smartassign.app.ui.theme.BgOverlay
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TypeBody

/**
 * Lector de código de barras/QR compartido, **en el dispositivo** — el
 * modelo de ML Kit va empaquetado en el APK (`com.google.mlkit:barcode-
 * scanning`, no la variante de Play Services que lo descarga la primera
 * vez), así que la imagen nunca sale del teléfono (00 §E1, §12.1).
 *
 * Dos usos distintos leen el mismo componente: la alta de dispositivo por
 * QR (02 §1.0, E6.3 — decodifica una URL) y el escáner de gafete
 * (§12.2, E6.5 — decodifica un número de ficha). Este componente no sabe
 * ni le importa qué significa el texto que decodificó; solo lo entrega
 * una vez por visita a la pantalla.
 */
@Composable
fun EscaneoQrScreen(
    instruccion: String,
    onCodigoDetectado: (String) -> Unit,
    modifier: Modifier = Modifier
) {
    val contexto = LocalContext.current
    var permisoConcedido by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(contexto, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED
        )
    }
    val lanzadorPermiso = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { concedido ->
        permisoConcedido = concedido
    }
    LaunchedEffect(Unit) {
        if (!permisoConcedido) lanzadorPermiso.launch(Manifest.permission.CAMERA)
    }

    if (permisoConcedido) {
        VistaCamara(instruccion, onCodigoDetectado, modifier)
    } else {
        VistaSinPermiso(modifier) { lanzadorPermiso.launch(Manifest.permission.CAMERA) }
    }
}

@Composable
private fun VistaCamara(
    instruccion: String,
    onCodigoDetectado: (String) -> Unit,
    modifier: Modifier
) {
    val contexto = LocalContext.current
    val ciclosDeVida = LocalLifecycleOwner.current
    var yaDetectado by remember { mutableStateOf(false) }
    val onCodigoDetectadoActual = rememberUpdatedState(onCodigoDetectado)

    Box(modifier = modifier.fillMaxSize().testTag("escaneo-qr")) {
        AndroidView(
            modifier = Modifier.fillMaxSize(),
            factory = { ctx ->
                val vistaPrevia = PreviewView(ctx)
                val proveedor = ProcessCameraProvider.getInstance(ctx)
                proveedor.addListener({
                    val camara = proveedor.get()
                    val preview = Preview.Builder().build().also { it.surfaceProvider = vistaPrevia.surfaceProvider }
                    val analizador = ImageAnalysis.Builder()
                        .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                        .build()
                    val escaner = BarcodeScanning.getClient()
                    analizador.setAnalyzer(ContextCompat.getMainExecutor(ctx)) { frame ->
                        procesarFrame(frame, escaner) { valor ->
                            if (!yaDetectado) {
                                yaDetectado = true
                                onCodigoDetectadoActual.value(valor)
                            }
                        }
                    }
                    camara.unbindAll()
                    camara.bindToLifecycle(ciclosDeVida, CameraSelector.DEFAULT_BACK_CAMERA, preview, analizador)
                }, ContextCompat.getMainExecutor(ctx))
                vistaPrevia
            }
        )
        Text(
            text = instruccion,
            style = TypeBody,
            color = TextPrimary,
            modifier = Modifier
                .align(Alignment.BottomCenter)
                .fillMaxWidth()
                .background(BgOverlay)
                .padding(Spacing.lg)
        )
    }
}

@OptIn(ExperimentalGetImage::class)
private fun procesarFrame(
    frame: ImageProxy,
    escaner: com.google.mlkit.vision.barcode.BarcodeScanner,
    onValor: (String) -> Unit
) {
    val imagenNativa = frame.image
    if (imagenNativa == null) {
        frame.close()
        return
    }
    val entrada = InputImage.fromMediaImage(imagenNativa, frame.imageInfo.rotationDegrees)
    escaner.process(entrada)
        .addOnSuccessListener { codigos ->
            codigos.firstOrNull { it.format == Barcode.FORMAT_QR_CODE }?.rawValue?.let(onValor)
        }
        .addOnCompleteListener { frame.close() }
}

@Composable
private fun VistaSinPermiso(modifier: Modifier, onReintentar: () -> Unit) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .testTag("escaneo-sin-permiso")
            .padding(Spacing.xl),
        verticalArrangement = androidx.compose.foundation.layout.Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text("Se necesita la cámara para escanear.", style = TypeBody, color = TextPrimary)
        Button(onClick = onReintentar, modifier = Modifier.padding(top = Spacing.md)) {
            Text("Permitir cámara")
        }
    }
}
