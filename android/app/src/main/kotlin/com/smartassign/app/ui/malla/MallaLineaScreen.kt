package com.smartassign.app.ui.malla

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.wrapContentHeight
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.ui.asignacion.FlujoAsignacionPorFicha
import com.smartassign.app.ui.escaneogafete.EscaneoGafeteScreen
import com.smartassign.app.ui.estado.EstadoPantalla
import com.smartassign.app.ui.estado.PantallaConEstado
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeLabel

/**
 * 03 §3.1/§4.3: la malla de línea — el corazón de la app del supervisor.
 *
 * El botón de escanear (00 §E1) es la puerta de entrada al llenado real
 * (→ PC-3, 07 §6): abre el lector compartido (E6.5), y con la ficha ya
 * decodificada arma el resto del recorrido — identidad (E6.6), sugerencia
 * (E6.7) y asignación (E6.8) — en [FlujoAsignacionPorFicha]. Al cerrarse
 * ese recorrido, sea por éxito o por cancelación, se vuelve a cargar la
 * malla: si hubo asignación real, es la única confirmación visible que
 * necesita el supervisor.
 */
@Composable
fun MallaLineaScreen(viewModel: MallaLineaViewModel = hiltViewModel()) {
    val estado by viewModel.estado.collectAsState()
    var mostrandoEscaneo by remember { mutableStateOf(false) }
    var fichaEnConfirmacion by remember { mutableStateOf<String?>(null) }

    if (mostrandoEscaneo) {
        EscaneoGafeteScreen(onFichaDecodificada = { ficha ->
            mostrandoEscaneo = false
            fichaEnConfirmacion = ficha
        })
        return
    }

    Box(modifier = Modifier.fillMaxSize()) {
        PantallaConEstado(
            estado = estado,
            esqueleto = { EsqueletoDeMalla() },
            contenido = { puestos -> ListaAgrupada(puestos) }
        )

        FloatingActionButton(
            onClick = { mostrandoEscaneo = true },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(Spacing.lg)
                // 03 §5.1: acción primaria de la pantalla central de la app
                // (§3.1) — 64 dp, no el tamaño de FAB por defecto de
                // Material3 (56 dp). E14.5, hallazgo real vía prueba.
                .size(TouchTarget.accionPrimaria)
                .testTag("malla-boton-escanear-gafete")
        ) {
            Icon(Icons.Filled.QrCodeScanner, contentDescription = "Escanear gafete")
        }
    }

    fichaEnConfirmacion?.let { ficha ->
        val puestosActuales = (estado as? EstadoPantalla.Listo<List<PuestoMalla>>)?.datos.orEmpty()
        FlujoAsignacionPorFicha(
            ficha = ficha,
            puestosDeLinea = puestosActuales,
            onTerminado = {
                fichaEnConfirmacion = null
                viewModel.cargar()
            }
        )
    }
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
        contentPadding = PaddingValues(Spacing.md),
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
    // 03 §5.1: piso absoluto de 48 dp — Spacing.sm (8 dp) de relleno
    // vertical por sí solo no lo alcanzaba. E14.5, hallazgo real vía prueba.
    Text(
        text = "$cantidad puesto${if (cantidad == 1) "" else "s"} no requerido${if (cantidad == 1) "" else "s"} por el SKU de hoy",
        style = TypeLabel,
        color = TextSecondary,
        modifier = Modifier
            .fillMaxWidth()
            .heightIn(min = TouchTarget.minimo)
            .clickable(onClick = onClick)
            .wrapContentHeight(Alignment.CenterVertically)
            .padding(vertical = Spacing.sm)
            .testTag("malla-fuera-de-operacion-colapso")
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
