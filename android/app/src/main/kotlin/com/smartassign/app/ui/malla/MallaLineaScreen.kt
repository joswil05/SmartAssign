package com.smartassign.app.ui.malla

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.ui.estado.PantallaConEstado
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TypeLabel

/** 03 §3.1/§4.3: la malla de línea — el corazón de la app del supervisor. */
@Composable
fun MallaLineaScreen(viewModel: MallaLineaViewModel = hiltViewModel()) {
    val estado by viewModel.estado.collectAsState()

    PantallaConEstado(
        estado = estado,
        esqueleto = { EsqueletoDeMalla() },
        contenido = { puestos -> ListaAgrupada(puestos) }
    )
}

@Composable
private fun ListaAgrupada(puestos: List<PuestoMalla>) {
    // 03 §4.3: fijos primero, rotativos después (el servidor ya los
    // entrega en ese orden por código); fuera de operación al final,
    // colapsados. El orden dentro de cada grupo nunca lo decide el
    // cliente — solo separa en baldes preservando el orden recibido.
    val activos = puestos.filter { it.situacion != "fuera_de_operacion" }
    val fueraDeOperacion = puestos.filter { it.situacion == "fuera_de_operacion" }
    val fijos = activos.filter { it.tipo == "fijo" }
    val rotativos = activos.filter { it.tipo != "fijo" }

    var colapsoExpandido by remember { mutableStateOf(false) }

    LazyColumn(
        modifier = Modifier.fillMaxSize().testTag("malla-linea-lista"),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm)
    ) {
        if (fijos.isNotEmpty()) {
            item { EncabezadoDeGrupo("PUESTOS FIJOS") }
            items(fijos, key = { it.id }) { TarjetaDePuesto(it) }
        }
        if (rotativos.isNotEmpty()) {
            item { EncabezadoDeGrupo("PUESTOS ROTATIVOS") }
            items(rotativos, key = { it.id }) { TarjetaDePuesto(it) }
        }
        if (fueraDeOperacion.isNotEmpty()) {
            item {
                FilaColapsoFueraDeOperacion(
                    cantidad = fueraDeOperacion.size,
                    expandido = colapsoExpandido,
                    onClick = { colapsoExpandido = !colapsoExpandido }
                )
            }
            if (colapsoExpandido) {
                items(fueraDeOperacion, key = { it.id }) { TarjetaDePuesto(it) }
            }
        }
    }
}

@Composable
private fun EncabezadoDeGrupo(texto: String) {
    Text(texto, style = TypeLabel, color = TextSecondary, modifier = Modifier.padding(top = Spacing.sm))
}

@Composable
private fun FilaColapsoFueraDeOperacion(cantidad: Int, expandido: Boolean, onClick: () -> Unit) {
    // Micro-copia literal de 03 §4.3: "N puestos no requeridos por el SKU de hoy".
    Text(
        text = "$cantidad puesto${if (cantidad == 1) "" else "s"} no requerido${if (cantidad == 1) "" else "s"} por el SKU de hoy",
        style = TypeLabel,
        color = TextSecondary,
        modifier = Modifier
            .fillMaxWidth()
            .testTag("malla-fuera-de-operacion-colapso")
            .clickable(onClick = onClick)
            .padding(vertical = Spacing.sm)
    )
}

@Composable
private fun EsqueletoDeMalla() {
    Column(
        modifier = Modifier.fillMaxSize().padding(Spacing.md),
        verticalArrangement = Arrangement.spacedBy(Spacing.sm)
    ) {
        repeat(6) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(96.dp)
                    .alpha(0.6f)
                    .background(BgSurface, RoundedCornerShape(Radius.md))
            )
        }
    }
}
