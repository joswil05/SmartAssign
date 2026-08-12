package com.smartassign.app.ui.paro

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Timer
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import com.smartassign.app.ui.theme.BgSurfaceRaised
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TypeDisplay
import androidx.compose.ui.unit.dp
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/** §11.1: "El supervisor debe... liberar a mi gente rotativa" arranca este cronómetro al confirmar. */
private const val INTERVALO_TICK_MS = 1_000L

/**
 * 03 §3.8 — barra fija, persistente en todas las pantallas mientras hay un
 * paro abierto. Quien la coloca (`GrafoDeNavegacion`) la deja FUERA del
 * contenido que cambia con la navegación, para que sobreviva a cualquier
 * pantalla — es la propia UT: no es un banner por pantalla, es uno solo
 * para toda la app.
 *
 * No dibuja nada si [paro] es `null` — "Solo desaparece al reanudar
 * producción explícitamente" (§11.1): no hay estado intermedio ni
 * animación de salida declarada en el LEE, así que no se inventa una.
 *
 * `03 §4.2` (zona de pantalla) dibuja este banner por encima de la
 * cabecera; ninguna de las dos existe todavía en el código (la cabecera
 * con línea/turno/sello de frescura y el banner de sin conexión — D4/D5 —
 * son trabajo de otra etapa). Por eso [GrafoDeNavegacion] lo cuelga hoy
 * en el único lugar que existe: la raíz de la app, por encima del
 * `NavHost`. Cuando la cabecera y el banner de conexión se construyan,
 * este debe quedar por debajo de ese banner (§3.8, "por debajo del banner
 * de conexión si ambos están activos") — anotado aquí para no perderlo.
 */
@Composable
fun CronometroDeParo(paro: ParoActivo?, modifier: Modifier = Modifier) {
    if (paro == null) return

    val ahora by produceState(initialValue = Instant.now(), key1 = paro) {
        while (isActive) {
            value = Instant.now()
            delay(INTERVALO_TICK_MS)
        }
    }

    Row(
        modifier = modifier
            .testTag("cronometro-de-paro")
            .fillMaxWidth()
            .background(BgSurfaceRaised)
            .border(width = 1.dp, color = ColorPeligro)
            .padding(horizontal = Spacing.md, vertical = Spacing.sm)
            .semantics { contentDescription = "Paro en curso" },
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(Icons.Filled.Timer, contentDescription = null, tint = ColorPeligro)
        Text(
            text = "PARO · ${paro.categoria.uppercase()}   ${formatearDuracion(paro.inicio, ahora)}",
            style = TypeDisplay,
            color = TextPrimary,
            modifier = Modifier
                .padding(start = Spacing.sm)
                .testTag("cronometro-de-paro-texto")
        )
    }
}

/**
 * `HH:MM:SS`, cifras tabulares (03 §3.8: "para que los dígitos no
 * bailen" — `type.display` en Type.kt ya usa una escala fija, sin más
 * tratamiento que pedir el LEE). Nunca negativo: si el reloj local se
 * adelantara al `inicio` recibido del servidor, se muestra 00:00:00 en
 * vez de un tiempo absurdo.
 */
fun formatearDuracion(inicio: Instant, ahora: Instant): String {
    val segundos = Duration.between(inicio, ahora).seconds.coerceAtLeast(0)
    val horas = segundos / 3600
    val minutos = (segundos % 3600) / 60
    val restoSegundos = segundos % 60
    return "%02d:%02d:%02d".format(horas, minutos, restoSegundos)
}
