package com.smartassign.app.ui.confirmacion

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.ui.comun.etiquetaCategoria
import com.smartassign.app.ui.estado.EstadoPantalla
import com.smartassign.app.ui.estado.PantallaConEstado
import com.smartassign.app.ui.theme.BgSurface
import com.smartassign.app.ui.theme.ColorMedico
import com.smartassign.app.ui.theme.Elevation
import com.smartassign.app.ui.theme.Radius
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TextPrimary
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeCaption
import com.smartassign.app.ui.theme.TypeMono
import com.smartassign.app.ui.theme.TypeSubtitle
import com.smartassign.app.ui.theme.TypeTitle

/**
 * 03 §3.3 — "componente de seguridad": el escaneo del gafete (E6.5) por
 * sí solo nunca asienta la asignación; este modal siempre media (00 §E1
 * §12.2). Recibe la ficha ya decodificada y el destino a mostrar —
 * quién dispara este modal y qué hace con la confirmación (consolidar la
 * asignación) queda fuera de esta UT: no existe todavía el endpoint que
 * la escribe (llega con E6.7/E6.8).
 *
 * `dismissOnBackPress = false` / `dismissOnClickOutside = false` — regla
 * 4 del §3.3: "no responde a un toque accidental de retorno".
 */
@Composable
fun ModalConfirmacionIdentidad(
    ficha: String,
    destinoPuesto: String,
    destinoTipo: String,
    onConfirmar: (PersonaConfirmacion) -> Unit,
    onCancelar: () -> Unit,
    viewModel: ConfirmacionIdentidadViewModel = hiltViewModel()
) {
    val estado by viewModel.estado.collectAsState()

    LaunchedEffect(ficha) { viewModel.cargar(ficha) }

    Dialog(
        onDismissRequest = onCancelar,
        properties = DialogProperties(dismissOnBackPress = false, dismissOnClickOutside = false)
    ) {
        Surface(
            shape = RoundedCornerShape(Radius.lg),
            color = BgSurface,
            tonalElevation = Elevation.nivel3,
            modifier = Modifier
                .testTag("modal-confirmacion-identidad")
                .fillMaxWidth()
        ) {
            Column(modifier = Modifier.padding(Spacing.lg)) {
                PantallaConEstado(
                    estado = estado,
                    esqueleto = { EsqueletoConfirmacion() },
                    contenido = { persona ->
                        ContenidoConfirmacion(
                            persona = persona,
                            destinoPuesto = destinoPuesto,
                            destinoTipo = destinoTipo,
                            onConfirmar = { onConfirmar(persona) }
                        )
                    }
                )

                // Único punto de salida, presente en todo estado
                // (cargando/error/listo) — regla 4 del §3.3: nunca
                // preseleccionado, nunca por accidente.
                OutlinedButton(
                    onClick = onCancelar,
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(top = Spacing.md)
                        .height(TouchTarget.accionSecundaria)
                        .testTag("modal-confirmacion-cancelar")
                ) { Text("Cancelar") }
            }
        }
    }
}

@Composable
private fun ContenidoConfirmacion(
    persona: PersonaConfirmacion,
    destinoPuesto: String,
    destinoTipo: String,
    onConfirmar: () -> Unit
) {
    Column(modifier = Modifier.fillMaxWidth().testTag("modal-confirmacion-contenido")) {
        Text("¿Es esta la persona?", style = TypeSubtitle, color = TextPrimary)

        Text(
            text = persona.nombreCompleto,
            style = TypeTitle,
            color = TextPrimary,
            modifier = Modifier.padding(top = Spacing.md).testTag("modal-confirmacion-nombre")
        )
        Text(
            text = "Ficha ${persona.ficha}",
            style = TypeMono,
            color = TextSecondary,
            modifier = Modifier.testTag("modal-confirmacion-ficha")
        )
        Text(
            text = etiquetaCategoria(persona.categoria),
            style = TypeBody,
            color = TextSecondary,
            modifier = Modifier.testTag("modal-confirmacion-categoria")
        )

        BloqueRestriccionesMedicas(persona.restriccionesMedicas)

        Text(
            text = "Destino: $destinoPuesto · $destinoTipo",
            style = TypeBody,
            color = TextPrimary,
            modifier = Modifier.padding(top = Spacing.md).testTag("modal-confirmacion-destino")
        )

        Button(
            onClick = onConfirmar,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.lg)
                .height(TouchTarget.accionPrimaria)
                .testTag("modal-confirmacion-confirmar")
        ) { Text("CONFIRMAR ASIGNACIÓN") }
    }
}

/**
 * Regla 1 del §3.3: "nunca se colapsa, nunca se abrevia, nunca aparece
 * bajo un 'ver más'". Regla 2: la ausencia de restricciones se afirma,
 * no se omite (§12.4) — el bloque existe igual, con su propio texto.
 */
@Composable
private fun BloqueRestriccionesMedicas(restricciones: List<String>) {
    Column(
        modifier = Modifier
            .fillMaxWidth()
            .padding(top = Spacing.md)
            .border(2.dp, ColorMedico, RoundedCornerShape(Radius.sm))
            .padding(Spacing.md)
            .testTag("modal-confirmacion-bloque-medico")
    ) {
        Text("⚕ RESTRICCIONES MÉDICAS ACTIVAS", style = TypeCaption, color = ColorMedico)
        if (restricciones.isEmpty()) {
            Text(
                "Sin restricciones médicas registradas",
                style = TypeBody,
                color = TextSecondary,
                modifier = Modifier.padding(top = Spacing.xs)
            )
        } else {
            restricciones.forEach { restriccion ->
                Text(
                    "· $restriccion",
                    style = TypeBody,
                    color = TextPrimary,
                    modifier = Modifier.padding(top = Spacing.xs)
                )
            }
        }
    }
}

@Composable
private fun EsqueletoConfirmacion() {
    Column(modifier = Modifier.fillMaxWidth().testTag("modal-confirmacion-esqueleto")) {
        Column(modifier = Modifier.fillMaxWidth().height(120.dp).background(BgSurface, RoundedCornerShape(Radius.md))) {}
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.md)
                .height(80.dp)
                .background(BgSurface, RoundedCornerShape(Radius.md))
        ) {}
    }
}
