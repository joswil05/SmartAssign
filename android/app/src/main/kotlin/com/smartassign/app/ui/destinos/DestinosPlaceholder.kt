package com.smartassign.app.ui.destinos

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeTitle

/**
 * Raíces reales de cada rol tras autenticar (02 §1.1 último tramo).
 * Contenido real: Panel de Planta / Malla de línea (E6.4) / Panel Bolsón
 * (C7). Aquí solo existe la ruta y la prueba de que el enrutamiento llegó
 * al destino correcto — el contenido se construye en su propia etapa.
 */
@Composable
private fun DestinoPlaceholder(titulo: String, subtitulo: String, tag: String) {
    Column(
        modifier = Modifier.fillMaxSize().testTag(tag).padding(Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(titulo, style = TypeTitle)
        Text(subtitulo, style = TypeBody, modifier = Modifier.padding(top = Spacing.sm))
    }
}

@Composable
fun PanelPlantaScreen() = DestinoPlaceholder("Panel de Planta", "Coordinador — llega en una etapa posterior", "panel-planta")

@Composable
fun MallaLineaScreen() = DestinoPlaceholder("Malla de línea", "Se construye en la etapa E6.4", "malla-linea")

@Composable
fun PanelBolsonScreen() = DestinoPlaceholder("Panel del Bolsón", "L8 — sin malla de puestos (C7)", "panel-bolson")

/** §2.3: rama terminal — no hay ninguna acción posible sin línea asignada. */
@Composable
fun SinLineaScreen() = DestinoPlaceholder(
    "Sin línea asignada",
    "No tienes línea asignada. Habla con el Coordinador.",
    "sin-linea"
)
