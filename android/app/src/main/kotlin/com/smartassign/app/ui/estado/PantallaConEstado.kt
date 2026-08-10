package com.smartassign.app.ui.estado

import androidx.compose.animation.core.LinearEasing
import androidx.compose.animation.core.RepeatMode
import androidx.compose.animation.core.animateFloat
import androidx.compose.animation.core.infiniteRepeatable
import androidx.compose.animation.core.rememberInfiniteTransition
import androidx.compose.animation.core.tween
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Block
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.Inbox
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import com.smartassign.app.ui.theme.BgBase
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.EstadoFuera
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TypeBodyStrong
import com.smartassign.app.ui.theme.TypeCaption

/** Duración del pulso del esqueleto de carga — §3.11: "pulso 1.2 s". */
private const val DURACION_PULSO_MS = 1200

/**
 * Dibuja cualquier pantalla que dependa de datos remotos respetando los
 * cuatro tratamientos de docs/03_UIUX_BRIEF.md §3.11. El `when` es
 * exhaustivo a propósito — si se agrega un quinto caso a [EstadoPantalla],
 * este composable deja de compilar hasta que reciba su propio tratamiento.
 *
 * No existe una rama de respaldo tipo "spinner genérico": [esqueleto] es
 * obligatorio y es responsabilidad del llamador darle la forma del
 * contenido real (§12.4: "nunca solo un círculo girando").
 */
@Composable
fun <T> PantallaConEstado(
    estado: EstadoPantalla<T>,
    esqueleto: @Composable () -> Unit,
    contenido: @Composable (T) -> Unit
) {
    when (estado) {
        is EstadoPantalla.Cargando -> EsqueletoDeCarga(esqueleto)
        is EstadoPantalla.Listo -> contenido(estado.datos)
        is EstadoPantalla.VacioLegitimo -> VistaVacioLegitimo(estado)
        is EstadoPantalla.FueraDeOperacion -> VistaFueraDeOperacion(estado)
        is EstadoPantalla.Error -> VistaError(estado)
    }
}

/**
 * Esqueleto con la forma del contenido real, pulsando cada 1.2 s. El
 * llamador decide la forma (12 tarjetas, una lista, lo que sea) — este
 * composable solo le aplica el pulso y la etiqueta de accesibilidad.
 */
@Composable
private fun EsqueletoDeCarga(esqueleto: @Composable () -> Unit) {
    val transicion = rememberInfiniteTransition(label = "pulso-carga")
    val alfa by transicion.animateFloat(
        initialValue = 0.4f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(durationMillis = DURACION_PULSO_MS, easing = LinearEasing),
            repeatMode = RepeatMode.Reverse
        ),
        label = "alfa-pulso"
    )
    Box(
        modifier = Modifier
            .testTag("estado-cargando")
            .alpha(alfa)
            .semantics { contentDescription = "Cargando" }
    ) {
        esqueleto()
    }
}

@Composable
private fun VistaVacioLegitimo(estado: EstadoPantalla.VacioLegitimo) {
    Column(
        modifier = Modifier
            .testTag("estado-vacio-legitimo")
            .fillMaxWidth()
            .padding(Spacing.xl),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.sm)
    ) {
        Icon(imageVector = Icons.Filled.Inbox, contentDescription = null, tint = TextSecondary)
        Text(text = estado.mensaje, style = TypeBodyStrong, color = TextPrimary)
        Text(text = estado.siguientePaso, style = TypeCaption, color = TextSecondary)
    }
}

/**
 * Superficie hundida + diagonal (§2.1: "Fuera de operación"). No comparte
 * tratamiento con "vacío" ni con "error": el dato no aplica hoy, no es
 * que falte ni que algo haya salido mal.
 */
@Composable
private fun VistaFueraDeOperacion(estado: EstadoPantalla.FueraDeOperacion) {
    Column(
        modifier = Modifier
            .testTag("estado-fuera-de-operacion")
            .fillMaxWidth()
            .background(BgBase, RoundedCornerShape(Radius.md))
            .alpha(0.55f) // superficie hundida, §2.1
            .padding(Spacing.xl),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.sm)
    ) {
        Icon(imageVector = Icons.Filled.Block, contentDescription = null, tint = EstadoFuera)
        Text(text = "Fuera de operación", style = TypeBodyStrong, color = TextPrimary)
        Text(text = estado.motivo, style = TypeCaption, color = TextSecondary)
    }
}

@Composable
private fun VistaError(estado: EstadoPantalla.Error) {
    Column(
        modifier = Modifier
            .testTag("estado-error")
            .fillMaxWidth()
            .padding(Spacing.xl),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(Spacing.sm)
    ) {
        Icon(imageVector = Icons.Filled.ErrorOutline, contentDescription = null, tint = ColorPeligro)
        Text(text = estado.causa, style = TypeBodyStrong, color = TextPrimary)
        Text(text = estado.accionSugerida, style = TypeCaption, color = TextSecondary)
    }
}
