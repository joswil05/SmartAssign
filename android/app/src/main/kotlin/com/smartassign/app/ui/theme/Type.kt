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

/**
 * Rótulo de botón. 24 sp por dos razones que apuntan al mismo sitio:
 *
 * 1. **Contraste.** `03 §5.2` exige 7:1 a texto normal y 4.5:1 a texto
 *    ≥ 24 sp. Sobre `AccionPrimaria` el 7:1 es inalcanzable para
 *    cualquier color de texto sin que el botón desaparezca contra el
 *    fondo (la cuenta completa está en `Color.kt`). A 24 sp el umbral
 *    aplicable es 4.5:1, que el par actual supera con 5.15:1.
 * 2. **Proporción.** `03 §5.1` deja la acción primaria en 64 dp —el
 *    objetivo más grande de la app— *"porque es la que más se usa y la
 *    que menos puede fallar"*, y `§2.2` justifica el cuerpo en 18 sp
 *    porque *"se lee de pie, en movimiento, a distancia de brazo"*. Un
 *    rótulo de 16 sp (el penúltimo tamaño de la escala) dentro de un
 *    botón de 64 dp contradecía ese razonamiento: el control más
 *    importante llevaba de las letras más pequeñas.
 */
val TypeAction = TextStyle(fontSize = 24.sp, fontWeight = FontWeight.SemiBold)
val TypeMono = TextStyle(fontSize = 18.sp, fontWeight = FontWeight.Medium, fontFamily = FontFamily.Monospace)

/** Todos los tamaños declarados — usado para verificar el piso de 15 sp (§12.3). */
val TodosLosEstilosTipograficos = listOf(
    TypeDisplay, TypeTitle, TypeSubtitle, TypeBody, TypeBodyStrong, TypeCaption, TypeLabel, TypeAction, TypeMono
)

val SmartAssignTypography = Typography(
    displayLarge = TypeDisplay,
    headlineLarge = TypeTitle,
    titleLarge = TypeSubtitle,
    bodyLarge = TypeBody,
    bodyMedium = TypeBodyStrong,
    // Material3 pinta el rótulo de todo `Button`/`TextButton` con
    // `labelLarge`. Cambiarlo aquí alcanza a las ~12 pantallas de una
    // vez, sin tener que acordarse de pasar el estilo en cada llamada —
    // que es exactamente el tipo de olvido que dejó la app en
    // negro-sobre-negro.
    labelLarge = TypeAction,
    labelMedium = TypeLabel
)
