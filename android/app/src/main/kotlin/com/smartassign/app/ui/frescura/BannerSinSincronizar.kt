package com.smartassign.app.ui.frescura

import androidx.compose.foundation.background
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
import com.smartassign.app.ui.theme.ColorAlerta
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

    Row(
        modifier = modifier
            .testTag("banner-sin-sincronizar")
            .fillMaxWidth()
            .background(ColorAlerta)
            .padding(horizontal = Spacing.md, vertical = Spacing.sm)
            .semantics { contentDescription = "Pendiente de sincronización" },
        verticalAlignment = Alignment.CenterVertically
    ) {
        Icon(Icons.Filled.CloudOff, contentDescription = null)
        Text(
            text = "Pendiente de sincronización — no mover al personal hasta recuperar la red.",
            style = TypeBodyStrong,
            modifier = Modifier.padding(start = Spacing.sm)
        )
    }
}
