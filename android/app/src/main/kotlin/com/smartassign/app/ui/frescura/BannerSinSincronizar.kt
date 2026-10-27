package com.smartassign.app.ui.frescura

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CloudOff
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.semantics.contentDescription
import androidx.compose.ui.semantics.semantics
import androidx.compose.ui.unit.dp
import com.smartassign.app.ui.theme.ColorOfflineBg
import com.smartassign.app.ui.theme.ColorOfflineFg
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TypeBodyStrong

/**
 * UT-E13.4 (00 §D4, §12.1): el "aviso permanente e inequívoco" —
 * literal, §12.1: *"Pendiente de sincronización — no mover al personal
 * hasta recuperar la red."* Un solo componente para las DOS causas que
 * D4 describe con el mismo texto: sin conexión ahora mismo (E13.3,
 * [BannerSinSincronizarViewModel]) o un dato cacheado más viejo que
 * `ANTIGUEDAD_MAXIMA_DATOS_MIN` en la pantalla que se esté viendo —
 * ninguna pantalla llama todavía a la segunda, porque ninguna lee de la
 * caché de E13.2 todavía (hueco conocido, no de esta UT).
 */
@Composable
fun BannerSinSincronizar(visible: Boolean, modifier: Modifier = Modifier) {
    if (!visible) return

    // UT-E15.7 (08 §D-1, cerrada por el cliente): antes pintaba
    // `TextPrimary` sobre `ColorAlerta` — 2.72:1, por debajo incluso de
    // AA— y no usaba ninguno de los dos tokens que 03 §2.1 había definido
    // para este banner. El fondo se queda oscuro (el `#2B2113` del brief)
    // y quien define el bloque es el BORDE, no el fondo: con un fondo
    // claro suficiente para destacar (≥3:1 sobre la app) ningún texto
    // alcanza 7:1, la misma ventana vacía que resolvió A15.
    //
    // `mergeDescendants` es lo que hace que TalkBack lo anuncie UNA vez:
    // sin él, el `contentDescription` del contenedor convivía con el texto
    // y el aviso se leía dos veces.
    Row(
        modifier = modifier
            .testTag("banner-sin-sincronizar")
            .fillMaxWidth()
            .background(ColorOfflineBg)
            .border(width = 1.dp, color = ColorOfflineFg)
            .padding(horizontal = Spacing.md, vertical = Spacing.sm)
            .semantics(mergeDescendants = true) {
                contentDescription =
                    "Pendiente de sincronización. No mover al personal hasta recuperar la red."
            },
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(Icons.Filled.CloudOff, contentDescription = null, tint = ColorOfflineFg)
        Text(
            text = "Pendiente de sincronización — no mover al personal hasta recuperar la red.",
            style = TypeBodyStrong,
            color = ColorOfflineFg,
            modifier = Modifier.padding(start = Spacing.sm)
        )
    }
}
