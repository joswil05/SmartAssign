package com.smartassign.app.ui.theme

import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable

/**
 * SmartAssign nace en oscuro por defecto (menos deslumbramiento bajo
 * iluminación industrial variable — §12.3), pero ambos esquemas deben
 * cumplir el mismo piso de contraste AAA. No hay tema claro "de repuesto"
 * peor probado que el oscuro.
 */
private val DarkColors = darkColorScheme(
    background = BgBase,
    surface = BgSurface,
    surfaceVariant = BgSurfaceRaised,
    onBackground = TextPrimary,
    onSurface = TextPrimary,
    primary = AccionPrimaria,
    error = ColorPeligro,
    outline = BorderStrong,
    outlineVariant = BorderSubtle
)

private val LightColors = lightColorScheme(
    primary = AccionPrimaria,
    error = ColorPeligro
)

@Composable
fun SmartAssignTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    content: @Composable () -> Unit
) {
    val colorScheme = if (darkTheme) DarkColors else LightColors
    MaterialTheme(
        colorScheme = colorScheme,
        typography = SmartAssignTypography,
        content = content
    )
}
