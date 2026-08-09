package com.smartassign.app.ui.theme

import androidx.compose.material3.Typography
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.sp

/**
 * Escala tipográfica — ver docs/03_UIUX_BRIEF.md §2.2.
 *
 * Cuerpo en 18 sp, no los 14–16 habituales: se lee de pie, en movimiento,
 * a distancia de brazo, con la pantalla a brillo parcial (§12.3). Ningún
 * tamaño baja de 15 sp en toda la aplicación.
 */

val TypeDisplay = TextStyle(fontSize = 34.sp, fontWeight = FontWeight.Bold)
val TypeTitle = TextStyle(fontSize = 26.sp, fontWeight = FontWeight.Bold)
val TypeSubtitle = TextStyle(fontSize = 20.sp, fontWeight = FontWeight.SemiBold)
val TypeBody = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.Normal)
val TypeBodyStrong = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.SemiBold)
val TypeCaption = TextStyle(fontSize = 16.sp, fontWeight = FontWeight.Normal)
val TypeLabel = TextStyle(fontSize = 15.sp, fontWeight = FontWeight.SemiBold)

val SmartAssignTypography = Typography(
    displayLarge = TypeDisplay,
    headlineLarge = TypeTitle,
    titleLarge = TypeSubtitle,
    bodyLarge = TypeBody,
    bodyMedium = TypeBodyStrong,
    labelLarge = TypeCaption,
    labelMedium = TypeLabel
)
