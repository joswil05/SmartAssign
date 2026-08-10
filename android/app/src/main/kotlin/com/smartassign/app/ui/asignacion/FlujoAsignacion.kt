package com.smartassign.app.ui.asignacion

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.ui.confirmacion.ConfirmacionIdentidadViewModel
import com.smartassign.app.ui.confirmacion.ModalConfirmacionIdentidad
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.Elevation
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeBodyStrong
import com.smartassign.app.ui.theme.TypeCaption

/**
 * Encadena lo que E6.5 (escáner) ya entregó — una ficha decodificada —
 * con la identidad (E6.6), la sugerencia de destino (E6.7) y la
 * escritura atómica (E6.8), hasta que la malla pueda refrescarse con la
 * asignación real. No es una UT propia del plan (07 §7, nota de E6.8):
 * es la integración que falta para poder validar **PC-3** con un
 * teléfono real.
 *
 * `viewModelConfirmacion`: hueco de prueba para el `ModalConfirmacionIdentidad`
 * anidado (E6.6) — en producción se resuelve por Hilt igual que el resto;
 * en una prueba de Compose sin contenedor de Hilt hay que poder
 * suministrarlo explícito, mismo patrón que ya usa cada pantalla de esta
 * app (`viewModel: X = hiltViewModel()`).
 */
@Composable
fun FlujoAsignacionPorFicha(
    ficha: String,
    puestosDeLinea: List<PuestoMalla>,
    onTerminado: () -> Unit,
    viewModel: FlujoAsignacionViewModel = hiltViewModel(),
    viewModelConfirmacion: ConfirmacionIdentidadViewModel? = null
) {
    val estado by viewModel.estado.collectAsState()

    LaunchedEffect(ficha) { viewModel.iniciar(ficha, puestosDeLinea) }

    when (val actual = estado) {
        EstadoFlujoAsignacion.Cargando -> DialogoProcesando("Buscando un puesto disponible…")
        EstadoFlujoAsignacion.Confirmando -> DialogoProcesando("Confirmando asignación…")

        is EstadoFlujoAsignacion.ListoParaConfirmar -> ModalConfirmacionIdentidad(
            ficha = actual.ficha,
            destinoPuesto = actual.destinoPuesto,
            destinoTipo = actual.destinoTipo,
            onConfirmar = { viewModel.confirmar() },
            onCancelar = onTerminado,
            viewModel = viewModelConfirmacion ?: hiltViewModel()
        )

        // Nada que dibujar — el refresco de la malla (a cargo de quien
        // invoque este flujo) es la confirmación visible (E6.8, nota de
        // esta UT: "que la malla se refresque").
        is EstadoFlujoAsignacion.Confirmado -> LaunchedEffect(actual.asignacionId) { onTerminado() }

        is EstadoFlujoAsignacion.Error -> DialogoDeCierre(
            causa = actual.causa,
            accionSugerida = actual.accionSugerida,
            onCerrar = onTerminado
        )

        is EstadoFlujoAsignacion.RechazadoAlConfirmar -> DialogoDeCierre(
            causa = actual.mensaje,
            accionSugerida = "Vuelve a escanear el gafete para intentarlo de nuevo.",
            onCerrar = onTerminado
        )
    }
}

/**
 * Indicador de progreso para una escritura en curso — distinto del "círculo
 * girando" que §12.4 prohíbe para pantallas de *contenido* (ahí la forma
 * la da el esqueleto, EstadoPantalla.Cargando): esto es la confirmación de
 * que un toque de escritura sí se está procesando, siempre acompañado de
 * qué es lo que está pasando, nunca un spinner solo.
 */
@Composable
private fun DialogoProcesando(mensaje: String) {
    Dialog(onDismissRequest = {}, properties = DialogProperties(dismissOnBackPress = false, dismissOnClickOutside = false)) {
        Surface(
            shape = RoundedCornerShape(Radius.lg),
            color = BgSurface,
            tonalElevation = Elevation.nivel3,
            modifier = Modifier.testTag("flujo-asignacion-procesando").fillMaxWidth()
        ) {
            Row(
                modifier = Modifier.padding(Spacing.lg),
                verticalAlignment = Alignment.CenterVertically
            ) {
                CircularProgressIndicator(modifier = Modifier.padding(end = Spacing.md).testTag("flujo-asignacion-procesando-indicador"))
                Text(mensaje, style = TypeBody, color = TextPrimary, modifier = Modifier.testTag("flujo-asignacion-procesando-mensaje"))
            }
        }
    }
}

@Composable
private fun DialogoDeCierre(causa: String, accionSugerida: String, onCerrar: () -> Unit) {
    Dialog(onDismissRequest = onCerrar, properties = DialogProperties(dismissOnBackPress = false, dismissOnClickOutside = false)) {
        Surface(
            shape = RoundedCornerShape(Radius.lg),
            color = BgSurface,
            tonalElevation = Elevation.nivel3,
            modifier = Modifier.testTag("flujo-asignacion-error").fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(Spacing.lg)) {
                Icon(Icons.Filled.ErrorOutline, contentDescription = null, tint = ColorPeligro)
                Text(
                    text = causa,
                    style = TypeBodyStrong,
                    color = TextPrimary,
                    modifier = Modifier.padding(top = Spacing.sm).testTag("flujo-asignacion-error-causa")
                )
                Text(
                    text = accionSugerida,
                    style = TypeCaption,
                    color = TextSecondary,
                    modifier = Modifier.padding(top = Spacing.xs).testTag("flujo-asignacion-error-accion")
                )
                Button(
                    onClick = onCerrar,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = Spacing.lg)
                        .height(TouchTarget.accionSecundaria)
                        .testTag("flujo-asignacion-error-cerrar")
                ) { Text("Entendido") }
            }
        }
    }
}
