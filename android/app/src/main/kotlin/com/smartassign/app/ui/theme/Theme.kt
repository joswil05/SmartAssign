package com.smartassign.app.ui.theme

import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier

/**
 * SmartAssign es oscuro **siempre**, no "oscuro por defecto":
 * `03_UIUX_BRIEF.md §2` define un único juego de tokens y explica por qué
 * ("la planta tiene iluminación variable y el operador mira la pantalla
 * decenas de veces por turno"). Ningún documento describe una paleta
 * clara, así que no existe ninguna — inventarla sería inventar diseño
 * (R2). `res/values/themes.xml` ya fija `windowBackground = #0E1116` sin
 * condicionarlo al modo del sistema; el tema de Compose tiene que decir
 * lo mismo o los dos se contradicen.
 *
 * **Bug real que esto corrige** (encontrado mirando la app en el
 * emulador, no por una prueba): antes se elegía entre este esquema y un
 * `lightColorScheme` que solo definía `primary` y `error`. Con el sistema
 * en modo claro —el estado de fábrica de cualquier teléfono— Compose
 * caía al `onBackground` por defecto de Material3 (`#1C1B1F`) y lo
 * pintaba sobre el fondo oscuro fijo de `themes.xml`: **1.10:1 de
 * contraste, contra los 7:1 (AAA) que exige `03 §5.2` por `§12.3`**. El
 * texto era prácticamente invisible. Las 51 pruebas instrumentadas no lo
 * vieron porque `assertIsDisplayed()` consulta el árbol semántico —que
 * era correcto— y nunca evalúa el color con el que se rasteriza.
 */
private val DarkColors = darkColorScheme(
    background = BgBase,
    surface = BgSurface,
    surfaceVariant = BgSurfaceRaised,
    onBackground = TextPrimary,
    onSurface = TextPrimary,
    onSurfaceVariant = TextSecondary,
    primary = AccionPrimaria,
    // `onPrimary`/`onError` son el texto QUE VA ENCIMA de esos colores —
    // el rótulo de un botón primario, por ejemplo. Sin declararlos,
    // Material3 usa los suyos, pensados para su propia paleta y no para
    // la de 03 §2.1: sobre `AccionPrimaria` (#2F6FED) pintaba un azul
    // oscuro casi ilegible en vez de texto claro.
    onPrimary = TextPrimary,
    error = ColorPeligro,
    onError = TextPrimary,
    outline = BorderStrong,
    outlineVariant = BorderSubtle
)

@Composable
fun SmartAssignTheme(content: @Composable () -> Unit) {
    // Sin parámetro `darkTheme` y sin `isSystemInDarkTheme()` a propósito:
    // que el modo del teléfono pueda cambiar la paleta es justamente lo
    // que rompía el contraste. El supervisor no elige el tema de una
    // herramienta de planta.
    MaterialTheme(
        colorScheme = DarkColors,
        typography = SmartAssignTypography
    ) {
        // ── La mitad que faltaba, y la causa raíz de verdad ──
        // Los estilos de `Type.kt` (`TypeTitle`, `TypeBody`, …) declaran
        // tamaño y peso pero NO color, a propósito: el color es un token
        // aparte (03 §2.1). Cuando un `TextStyle` no trae color, Compose
        // cae a `LocalContentColor` — y `LocalContentColor` solo lo fija
        // un `Surface`/`Scaffold`. Como casi ninguna pantalla envolvía su
        // contenido en uno, quedaba el valor por defecto de Material3:
        // NEGRO, sobre el fondo #0E1116 que `themes.xml` pinta siempre.
        // Es decir: el tema tenía los tokens correctos y aun así la app
        // se veía negro-sobre-negro, porque nadie los conectaba al texto.
        // Este `Surface` los conecta de una vez para todas las pantallas.
        Surface(
            modifier = Modifier.fillMaxSize(),
            color = MaterialTheme.colorScheme.background,
            contentColor = MaterialTheme.colorScheme.onBackground,
            content = content
        )
    }
}
