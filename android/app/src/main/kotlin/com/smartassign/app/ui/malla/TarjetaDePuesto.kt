package com.smartassign.app.ui.malla

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
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.LocalHospital
import androidx.compose.material.icons.filled.Repeat
import androidx.compose.material.icons.filled.Schedule
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.draw.clip
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.ui.comun.etiquetaCategoria
import com.smartassign.app.ui.theme.BgBase
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.ColorMedico
import com.smartassign.app.ui.theme.EstadoCritico
import com.smartassign.app.ui.theme.EstadoDescubierto
import com.smartassign.app.ui.theme.EstadoFuera
import com.smartassign.app.ui.theme.EstadoLibre
import com.smartassign.app.ui.theme.EstadoOcupado
import com.smartassign.app.ui.theme.FatigaCritico
import com.smartassign.app.ui.theme.FatigaNormal
import com.smartassign.app.ui.theme.FatigaSugerido
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
 * contextual siempre presente, y desde E7.4 la barra de fatiga
 * (anatomía #6) — solo cuando `puesto.nivelFatiga` trae un dato real
 * (E7, `fn_NivelFatiga`): nunca se dibuja una barra inventada (§1.3).
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
                Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(Spacing.xs)) {
                    Text(ocupante.nombreCompleto, style = TypeSubtitle, color = TextPrimary)
                    // 00 §B7: distintivo permanente, informativo — nunca bloquea.
                    if (ocupante.dobleTurno) {
                        Icon(
                            Icons.Filled.Repeat, contentDescription = "Doble turno", tint = TextSecondary,
                            modifier = Modifier.size(16.dp).testTag("tarjeta-puesto-${puesto.id}-doble-turno")
                        )
                    }
                }
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

            puesto.nivelFatiga?.let { nivel ->
                BarraDeFatiga(nivel, puesto.excesoFatiga, modifier = Modifier.testTag("tarjeta-puesto-${puesto.id}-barra-fatiga"))
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

/**
 * §9.1/03 §2: avance continuo desde el minuto cero, relativo al umbral
 * propio (A4) — nunca minutos absolutos. Grosor y color por nivel
 * (normal fina, sugerido media + reloj, crítico gruesa + alerta + pulso
 * lento). `exceso` puede superar 100 (ya pasó el umbral) — el relleno se
 * acota a la pista, la cifra real vive en `excesoFatiga`, no en la barra.
 */
@Composable
private fun BarraDeFatiga(nivel: String, exceso: Double?, modifier: Modifier = Modifier) {
    val color = colorDeNivelFatiga(nivel)
    val grosor = when (nivel) {
        "critico" -> 8.dp
        "sugerido" -> 6.dp
        else -> 3.dp
    }
    val fraccionLlena = ((exceso ?: 0.0) / 100.0).coerceIn(0.0, 1.0).toFloat()

    val alfaPulso = if (nivel == "critico") {
        val transicion = rememberInfiniteTransition(label = "pulso-fatiga-critica")
        val alfa by transicion.animateFloat(
            initialValue = 0.5f,
            targetValue = 1f,
            animationSpec = infiniteRepeatable(tween(durationMillis = 2000, easing = LinearEasing), RepeatMode.Reverse),
            label = "alfa-pulso-fatiga-critica"
        )
        alfa
    } else {
        1f
    }

    Row(
        modifier = modifier.fillMaxWidth().padding(top = Spacing.xs),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(Spacing.xs)
    ) {
        when (nivel) {
            "sugerido" -> Icon(Icons.Filled.Schedule, contentDescription = null, tint = color, modifier = Modifier.size(14.dp))
            "critico" -> Icon(Icons.Filled.Warning, contentDescription = null, tint = color.copy(alpha = alfaPulso), modifier = Modifier.size(14.dp))
        }
        Box(
            modifier = Modifier
                .weight(1f)
                .height(grosor)
                .clip(RoundedCornerShape(Radius.pill))
                .background(BgBase)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxHeight()
                    .fillMaxWidth(fraccionLlena)
                    .clip(RoundedCornerShape(Radius.pill))
                    .background(color.copy(alpha = alfaPulso))
            )
        }
    }
}

private fun colorDeNivelFatiga(nivel: String) = when (nivel) {
    "critico" -> FatigaCritico
    "sugerido" -> FatigaSugerido
    else -> FatigaNormal
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
