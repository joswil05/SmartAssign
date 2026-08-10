package com.smartassign.app.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

/**
 * Escala tipográfica — ver docs/03_UIUX_BRIEF.md §2.2.
 *
 * Cuerpo en 18 sp, no los 14–16 habituales: se lee de pie, en movimiento,
 * a distancia de brazo, con la pantalla a brillo parcial (§12.3). Ningún
 * tamaño baja de 15 sp en toda la aplicación.
 *
 * Fuente: Roboto (default del sistema Android — no se declara `fontFamily`
 * explícito para que Compose use el system default y no dependa de una
 * descarga externa, §12.1). `type.mono` es la única excepción: usa el
 * monospace del sistema (Roboto Mono en la mayoría de dispositivos Android),
 * también resuelto localmente, nunca descargado.
 */

val TypeDisplay = TextStyle(fontSize = 34.sp, fontWeight = FontWeight.Bold)
val TypeTitle = TextStyle(fontSize = 26.sp, fontWeight = FontWeight.Bold)
val TypeSubtitle = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
val TypeBody = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.Normal)
val TypeBodyStrong = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.SemiBold)
val TypeCaption = TextStyle(fontSize = 16.sp, fontWeight = FontWeight.Normal)
val TypeLabel = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold)
val TypeMono = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.Medium, fontFamily = FontFamily.Monospace)

/** Todos los tamaños declarados — usado para verificar el piso de 15 sp (§12.3). */
val TodosLosEstilosTipograficos = listOf(
    TypeDisplay, TypeTitle, TypeSubtitle, TypeBody, TypeBodyStrong, TypeCaption, TypeLabel, TypeMono
)

val SmartAssignTypography = Typography(
    displayLarge = TypeDisplay,
    headlineLarge = TypeTitle,
    titleLarge = TypeSubtitle,
    bodyLarge = TypeBody,
    bodyMedium = TypeBodyStrong,
    labelLarge = TypeCaption,
    labelMedium = TypeLabel
)
