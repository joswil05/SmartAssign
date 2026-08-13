package com.smartassign.app.ui.frescura

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.rotate
import androidx.compose.ui.platform.testTag
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TypeLabel
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/**
 * UT-E13.4 (00 §D4, 03 §3.7): *"el contenido de datos se muestra al 60 %
 * de opacidad con la marca de agua diagonal"*. Por qué degradar y no
 * solo avisar, literal: *"un aviso se ignora; un contenido que se ve
 * distinto no. El objetivo es que sea físicamente imposible confundir
 * dato viejo con dato vivo"* (§12.4).
 *
 * `contenido` nunca se oculta ni se reemplaza — sigue siendo el dato
 * real, solo que innegablemente marcado. Ocultarlo violaría §12.1 ("una
 * terminal sin red debe verse y comportarse igual que una conectada").
 *
 * La marca de agua es un texto diagonal centrado, no un patrón repetido
 * en mosaico: el brief pide *"marca de agua diagonal"* sin especificar
 * repetición, y un solo texto rotado es igual de imposible de ignorar
 * sobre una tarjeta de tamaño de teléfono, además de quedar verificable
 * por una prueba de Compose (`onNodeWithTag`) en vez de solo por
 * inspección visual de píxeles.
 */
@Composable
fun ContenidoConDegradacion(cacheadoEn: Instant, modifier: Modifier = Modifier, contenido: @Composable () -> Unit) {
    val ahora by produceState(initialValue = Instant.now(), key1 = cacheadoEn) {
        while (isActive) {
            value = Instant.now()
            delay(15_000L)
        }
    }
    val enAlerta = nivelFrescura(cacheadoEn, ahora) == NivelFrescura.Alerta

    Box(modifier = modifier.testTag("contenido-con-degradacion")) {
        Box(modifier = Modifier.alpha(if (enAlerta) 0.6f else 1f)) {
            contenido()
        }
        if (enAlerta) {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                Text(
                    text = "SIN SINCRONIZAR",
                    style = TypeLabel,
                    color = TextSecondary,
                    modifier = Modifier
                        .testTag("marca-de-agua-sin-sincronizar")
                        .alpha(0.5f)
                        .rotate(-25f)
                )
            }
        }
    }
}
