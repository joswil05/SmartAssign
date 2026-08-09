package com.smartassign.app.ui.theme

import androidx.compose.ui.graphics.Color

/**
 * Tokens de color — ver docs/03_UIUX_BRIEF.md §2.1.
 *
 * Base oscura, validada en el peor caso de iluminación de planta, no en
 * el mejor. El contraste mínimo es AAA (7:1) para texto normal (§12.3, A11).
 * No se relaja aquí: cualquier cambio de valor debe volver a verificarse
 * contra ese umbral antes de mezclarse.
 */

// Base
val BgBase = Color(0xFF0E1116)
val BgSurface = Color(0xFF171C23)
val BgSurfaceRaised = Color(0xFF212832)
val BorderSubtle = Color(0xFF2C3540)
val BorderStrong = Color(0xFF455161)
val TextPrimary = Color(0xFFF4F7FA)
val TextSecondary = Color(0xFFB4BFCC)
val TextDisabled = Color(0xFF6B7684)

// Estados de puesto (§5.3, C11)
val EstadoLibre = Color(0xFF4A90D9)
val EstadoOcupado = Color(0xFF3FA76A)
val EstadoCritico = Color(0xFFE5484D)
val EstadoDescubierto = Color(0xFFD9822B)
val EstadoFuera = Color(0xFF5A6472)

// Fatiga (§9.1) — relativa al umbral propio del puesto (A4), nunca absoluta
val FatigaNormal = Color(0xFF3FA76A)
val FatigaSugerido = Color(0xFFD9822B)
val FatigaCritico = Color(0xFFE5484D)

// Semánticos de sistema
val AccionPrimaria = Color(0xFF2F6FED)
val ColorExito = Color(0xFF3FA76A)
val ColorAlerta = Color(0xFFD9822B)
val ColorPeligro = Color(0xFFE5484D)
val ColorMedico = Color(0xFFB5179E) // exclusivo de restricciones médicas — §7.2, §12.2
val ColorTransito = Color(0xFF7B5CD6)
