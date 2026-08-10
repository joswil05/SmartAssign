package com.smartassign.app.ui.arranque

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.ui.estado.PantallaConEstado
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TypeDisplay

@Composable
fun ArranqueScreen(
    onNavegarA: (String) -> Unit,
    viewModel: ArranqueViewModel = hiltViewModel()
) {
    val estado by viewModel.estado.collectAsState()

    PantallaConEstado(
        estado = estado,
        esqueleto = { EsqueletoArranque() },
        contenido = { ruta -> LaunchedEffect(ruta) { onNavegarA(ruta) } }
    )
}

@Composable
private fun EsqueletoArranque() {
    Column(
        modifier = Modifier.fillMaxSize().testTag("arranque-cargando"),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text("SmartAssign", style = TypeDisplay)
        CircularProgressIndicator(modifier = Modifier.padding(top = Spacing.lg))
    }
}
