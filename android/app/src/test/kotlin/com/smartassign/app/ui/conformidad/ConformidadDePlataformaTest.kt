package com.smartassign.app.ui.conformidad

import java.io.File
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * UT-E15.1 y UT-E15.2 (docs/08_AUDITORIA_FRONTEND.md §P0-1, §P0-3).
 *
 * **Qué prueba esto y qué no.** Es una guarda de contrato sobre el código
 * fuente, no una prueba de comportamiento: comprueba que ciertas
 * invariantes estructurales de toda la app siguen escritas donde tienen
 * que estar. No sustituye a la verificación en un teléfono real, y no
 * pretende hacerlo.
 *
 * **Por qué así, y no con una prueba de composición.** Las dos invariantes
 * que cubre son de *aplicación entera*, no de componente:
 *
 * - `enableEdgeToEdge()` es un compromiso que se adquiere en la `Activity`
 *   y se paga en la raíz del árbol de Compose. Una prueba de composición
 *   sobre un componente suelto no puede ver ese contrato, porque el
 *   componente nunca conoce los insets del sistema; y montar la `Activity`
 *   real exigiría un corredor de Hilt que esta suite no tiene.
 * - La ausencia de `verticalScroll` no rompe ninguna aserción de Compose:
 *   el contenido simplemente se recorta fuera de la ventana. Una prueba
 *   instrumentada pasaría en verde con el botón inalcanzable — que es
 *   literalmente el defecto que se está corrigiendo.
 *
 * Es el mismo razonamiento de `TemaAplicadoContrasteTest`: cuando la prueba
 * obvia no puede ver el fallo, se prueba el escalón donde el fallo sí es
 * visible.
 */
class ConformidadDePlataformaTest {

    private fun fuente(rutaRelativa: String): String {
        val candidatos = listOf(
            File("src/main/kotlin/com/smartassign/app/$rutaRelativa"),
            File("app/src/main/kotlin/com/smartassign/app/$rutaRelativa"),
            File("android/app/src/main/kotlin/com/smartassign/app/$rutaRelativa"),
        )
        val encontrado = candidatos.firstOrNull { it.exists() }
        assertTrue(
            "no se encontró $rutaRelativa desde ${File(".").absolutePath} — " +
                "si el fuente se movió, actualizar esta prueba",
            encontrado != null
        )
        return encontrado!!.readText()
    }

    /**
     * `MainActivity` llama a `enableEdgeToEdge()`, que le dice al sistema
     * que la app dibujará por debajo de la barra de estado y de la de
     * navegación **y asume el compromiso de apartar el contenido ella
     * misma**. Sin ese apartado, el banner de sin conexión —el aviso que
     * §12.1 exige *"permanente e inequívoco"*— quedaba parcialmente bajo
     * el reloj del sistema.
     */
    @Test
    fun `si la app dibuja de borde a borde, la raiz aparta los insets del sistema`() {
        val main = fuente("MainActivity.kt")

        if (!main.contains("enableEdgeToEdge()")) return // sin el compromiso, no hay deuda

        assertTrue(
            "MainActivity llama a enableEdgeToEdge() pero la raíz no aplica " +
                "safeDrawingPadding(): el contenido queda bajo la barra de estado y la de " +
                "navegación (08 §P0-1)",
            main.contains("safeDrawingPadding()")
        )
    }

    /**
     * Las dos únicas pantallas con campos de texto. Sin `imePadding()` el
     * teclado tapa el botón primario; sin `verticalScroll` no hay forma de
     * recuperarlo, ni de alcanzar el contenido a 130 % de escala de fuente
     * —que `03 §2.2` promete soportar—.
     */
    @Test
    fun `toda pantalla con campos de texto hace scroll y aparta el teclado`() {
        listOf(
            "ui/login/LoginScreen.kt",
            "ui/pin/PinScreen.kt",
        ).forEach { ruta ->
            val codigo = fuente(ruta)

            assertTrue(
                "$ruta tiene campos de texto pero no llama a imePadding(): el teclado " +
                    "tapa el botón primario y no hay forma de alcanzarlo (08 §P0-3)",
                codigo.contains("imePadding()")
            )
            assertTrue(
                "$ruta no llama a verticalScroll(): el contenido se recorta en pantalla " +
                    "corta y al subir la escala de fuente, sin recuperación posible " +
                    "(08 §P0-3, 03 §2.2)",
                codigo.contains("verticalScroll(")
            )
        }
    }

    /**
     * `03 §2.2` promete que *"el escalado tipográfico del sistema se
     * respeta hasta el 130 % sin que se rompa ningún layout"*.
     * `Modifier.height()` fija una altura **exacta**: el rótulo crece con
     * la escala, la caja no, y el texto se recorta. `heightIn(min = …)`
     * conserva intacto el piso de toque de A11 y deja crecer al botón.
     */
    @Test
    fun `ningun control fija una altura exacta con un token de zona de toque`() {
        val raiz = listOf(File("src/main/kotlin"), File("app/src/main/kotlin"), File("android/app/src/main/kotlin"))
            .firstOrNull { it.exists() }
        assertTrue("no se encontró el árbol de fuentes", raiz != null)

        val infractores = raiz!!.walkTopDown()
            .filter { it.isFile && it.extension == "kt" }
            .filter { it.readText().contains(".height(TouchTarget.") }
            .map { it.name }
            .toList()

        assertTrue(
            "estos ficheros fijan altura exacta con .height(TouchTarget.…) en vez de " +
                ".heightIn(min = TouchTarget.…): a 130 % de escala el rótulo se recorta " +
                "(08 §P1-7) — $infractores",
            infractores.isEmpty()
        )
    }
}
