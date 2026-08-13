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
val BgOverlay = Color(0xB8000000) // #000000 @ 72% — fondo de modal
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
/**
 * Azul de acción. **Oscurecido respecto del #2F6FED original** —mismo
 * tono (220°) y misma saturación (84 %), solo más profundo— para subir
 * el contraste del rótulo del botón de 4.19:1 a 5.15:1 (+23 %).
 *
 * Por qué no se oscureció más, con números: `03 §5.2` pide 7:1 para
 * texto normal y 3:1 para elementos de interfaz. Sobre el fondo
 * `BgBase` (#0E1116, L=0.0055) las dos reglas se excluyen — el texto
 * exigiría L ≤ 0.100 (aun con blanco puro) y el botón exige L ≥ 0.1166
 * para no perderse contra el fondo. **La ventana es vacía: ningún color
 * cumple las dos.** Este valor (L=0.1257) es el punto que maximiza el
 * contraste del rótulo conservando 3.42:1 de botón contra fondo, con
 * margen real sobre el mínimo de 3:1.
 *
 * El 7:1 se alcanza por el otro lado: el rótulo pasó a `TypeAction`
 * (24 sp), y `03 §5.2` fija 4.5:1 para texto ≥ 24 sp — umbral que
 * 5.15:1 supera con holgura. Ver `Type.kt`.
 */
val AccionPrimaria = Color(0xFF145DEB)
val ColorExito = Color(0xFF3FA76A)
val ColorAlerta = Color(0xFFD9822B)
val ColorPeligro = Color(0xFFE5484D)
val ColorMedico = Color(0xFFB5179E) // exclusivo de restricciones médicas — §7.2, §12.2
val ColorTransito = Color(0xFF7B5CD6)
val ColorOfflineFg = Color(0xFF8A6D3B) // banner de sin conexión — texto/icono
val ColorOfflineBg = Color(0xFF2B2113) // banner de sin conexión — fondo
