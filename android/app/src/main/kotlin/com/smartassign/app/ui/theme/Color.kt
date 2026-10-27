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

/**
 * Borde de componente interactivo — **aclarado de `#455161` a `#617288`**
 * (E15.3). Mismo tono (214°) y misma saturación (17 %), solo más claro.
 *
 * Por qué: Material3 lo recibe como `outline`, así que es el borde de todo
 * `OutlinedTextField` — y el borde **es** la única afordancia visual del
 * campo. El valor anterior daba 2.34:1 sobre `BgBase` y 2.12:1 sobre
 * `BgSurface`, por debajo del 3:1 que `03 §5.2` exige a los elementos de
 * interfaz. Un campo cuyo borde no se distingue no se ve.
 *
 * El valor nuevo cumple en las **tres** superficies (3.85 / 3.48 / 3.02),
 * incluida `BgSurfaceRaised`, que es el peor caso por ser la más clara.
 */
val BorderStrong = Color(0xFF617288)
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

/**
 * ── Variantes de TEXTO de los semánticos (E15.3) ──
 *
 * **Por qué existen, y por qué no basta con mover el token original.**
 *
 * `ColorMedico`, `ColorPeligro` y `ColorAlerta` hacen dos trabajos con dos
 * umbrales distintos de `03 §5.2`: como franja, relleno o icono son
 * *elementos de interfaz* (3:1), y como color de un `Text` son *texto
 * normal* (7:1). Sus valores de identidad cumplen el primero y fallan el
 * segundo — medido, no estimado:
 *
 * | Token           | sobre `BgBase` | sobre `BgSurface` | exige |
 * |-----------------|----------------|-------------------|-------|
 * | `ColorMedico`   | 3.22:1         | **2.92:1**        | 7:1   |
 * | `ColorPeligro`  | 4.83:1         | 4.37:1            | 7:1   |
 * | `ColorAlerta`   | 6.46:1         | 5.85:1            | 7:1   |
 *
 * El caso médico es el más grave: no alcanza ni el 4.5:1 de AA, y `§2.1`
 * justifica que ese color exista precisamente porque *"es la única
 * información de la app cuyo malentendido produce daño físico a una
 * persona"*.
 *
 * Mover el token original a un valor legible lo volvería pastel y le
 * quitaría la identidad que `§2.1` le pide. **Es la misma estructura de
 * A15**: el problema no se arregla con un solo valor, se arregla
 * partiéndolo en las dos mitades que de verdad tiene. La identidad se
 * queda arriba, para rellenos; estas variantes cubren el texto.
 *
 * Cada una conserva tono y saturación exactos del original —solo sube la
 * luminosidad— y cumple 7:1 en las **tres** superficies, incluida
 * `BgSurfaceRaised`, que es el peor caso por ser la más clara.
 */
val ColorMedicoTexto = Color(0xFFF195E4)  // 309°/77% — 9.15 / 8.28 / 7.18
val ColorPeligroTexto = Color(0xFFF19C9E) // 358°/75% — 9.00 / 8.14 / 7.06
val ColorAlertaTexto = Color(0xFFE4A668)  // 30°/70%  — 8.96 / 8.11 / 7.04

/**
 * Banner de sin conexión (D-1, cerrada por el cliente en E15.7).
 *
 * **El par que traía `03 §2.1` no cumplía su propia norma**: `#8A6D3B`
 * sobre `#2B2113` da 3.26:1, y el rótulo va en `TypeBodyStrong` (18 sp),
 * que por `§5.2` es texto normal y exige 7:1. El código, además, ni
 * siquiera usaba estos tokens: pintaba `TextPrimary` sobre `ColorAlerta`
 * — **2.72:1**, por debajo incluso de AA.
 *
 * **Y aquí se repite la aritmética de A15, literalmente.** Para que el
 * banner lea como un bloque distinguible del fondo de la app hace falta
 * un fondo a 3:1 sobre `BgBase`, o sea luminancia ≥ 0.1165; para que el
 * texto sobre ese fondo llegue a 7:1 haría falta luminancia ≥ 1.116.
 * **Mayor que 1: la ventana vuelve a estar vacía, ningún par cumple las
 * dos.**
 *
 * Se resuelve en dos mitades, igual que A15: el fondo se queda oscuro
 * —el `#2B2113` que el brief eligió— y quien define el bloque es un
 * **borde** en el color de primer plano, no el propio fondo. Es el mismo
 * recurso que ya usa `CronometroDeParo` (superficie + borde de 1 dp), así
 * que no introduce un patrón nuevo.
 */
val ColorOfflineFg = Color(0xFFE8C88A) // texto, icono y borde — 9.82:1 sobre el fondo del banner
val ColorOfflineBg = Color(0xFF2B2113) // fondo del banner, tal como lo fijó 03 §2.1
