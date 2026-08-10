package com.smartassign.app.ui.malla

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.LocalHospital
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.ui.comun.etiquetaCategoria
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.ColorMedico
import com.smartassign.app.ui.theme.EstadoCritico
import com.smartassign.app.ui.theme.EstadoDescubierto
import com.smartassign.app.ui.theme.EstadoFuera
import com.smartassign.app.ui.theme.EstadoLibre
import com.smartassign.app.ui.theme.EstadoOcupado
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeCaption
import com.smartassign.app.ui.theme.TypeLabel
import com.smartassign.app.ui.theme.TypeMono
import com.smartassign.app.ui.theme.TypeSubtitle

/**
 * El componente central de la app (03 §3.1) — aparece 8 a 20 veces por
 * pantalla. Anatomía obligatoria: franja lateral con el color del estado,
 * identificador en `type.mono`, tipo siempre visible, ocupante (o el
 * vacío correspondiente), indicador médico si aplica, micro-copia
 * contextual siempre presente. La barra de fatiga (anatomía #6) queda
 * fuera de esta UT — no existe motor de fatiga todavía (E7); no se
 * dibuja una barra que no tendría un número real detrás (§1.3).
 */
@Composable
fun TarjetaDePuesto(puesto: PuestoMalla, modifier: Modifier = Modifier) {
    val esFueraDeOperacion = puesto.situacion == "fuera_de_operacion"

    Row(
        modifier = modifier
            .fillMaxWidth()
            .testTag("tarjeta-puesto-${puesto.id}")
            .clip(RoundedCornerShape(Radius.md))
            .background(BgSurface)
            .then(if (esFueraDeOperacion) Modifier.alpha(0.55f) else Modifier) // §2.1: superficie hundida
    ) {
        if (!esFueraDeOperacion) {
            Box(
                Modifier
                    .width(anchoDeFranja(puesto.situacion))
                    .fillMaxHeight()
                    .background(colorDeSituacion(puesto.situacion))
            )
        }

        Column(modifier = Modifier.padding(Spacing.md)) {
            Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                Text(puesto.codigo, style = TypeMono, color = TextPrimary)
                Text(etiquetaTipo(puesto.tipo), style = TypeLabel, color = TextSecondary)
            }

            val ocupante = puesto.ocupante
            if (ocupante != null) {
                Text(ocupante.nombreCompleto, style = TypeSubtitle, color = TextPrimary,
                    modifier = Modifier.padding(top = Spacing.xs))
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.sm)) {
                    Text("Ficha ${ocupante.ficha} · ${etiquetaCategoria(ocupante.categoria)}", style = TypeBody, color = TextSecondary)
                    if (puesto.indicadorMedico > 0) {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Icon(Icons.Filled.LocalHospital, contentDescription = null, tint = ColorMedico)
                            Text(" ${puesto.indicadorMedico}", style = TypeBody, color = ColorMedico)
                        }
                    }
                }
            }

            Text(
                text = puesto.microCopia,
                style = TypeCaption,
                color = TextSecondary,
                modifier = Modifier.padding(top = Spacing.xs).testTag("tarjeta-puesto-${puesto.id}-microcopia")
            )
        }
    }
}

private fun anchoDeFranja(situacion: String) = when (situacion) {
    "vacante_critica" -> 8.dp   // "borde sólido grueso + franja lateral" — §2.1
    "descubierto" -> 6.dp       // "borde discontinuo grueso" — §2.1
    else -> 4.dp
}

private fun colorDeSituacion(situacion: String) = when (situacion) {
    "libre" -> EstadoLibre
    "ocupado" -> EstadoOcupado
    "vacante_critica" -> EstadoCritico
    "descubierto" -> EstadoDescubierto
    else -> EstadoFuera
}

private fun etiquetaTipo(tipo: String) = if (tipo == "fijo") "Fijo" else "Rotativo"
