package com.smartassign.app.ui.theme

import androidx.compose.ui.unit.dp

/**
 * Espaciado y zonas de toque — docs/03_UIUX_BRIEF.md §2.3, §5.1.
 *
 * Los tamaños de toque bajaron de 56/12 dp a 48/8 dp cuando el cliente
 * descartó el uso con guantes (decisión A11 del registro). La acción
 * primaria se queda en 64 dp de todas formas: es la que más se usa y
 * estas operaciones no son repetibles sin consecuencia (§12.4).
 */
object Spacing {
    val xs = 4.dp
    val sm = 8.dp
    val md = 16.dp
    val lg = 24.dp
    val xl = 32.dp
    val touch = 8.dp // separación mínima entre dos zonas de toque (A11)
}

object TouchTarget {
    val minimo = 48.dp   // piso absoluto, cualquier elemento (A11)
    val fila = 72.dp     // fila de lista tocable
    val accionPrimaria = 64.dp
    val accionSecundaria = 56.dp
    val accionTerciaria = 48.dp
}

object Radius {
    val sm = 8.dp
    val md = 12.dp
    val lg = 20.dp
    val pill = 999.dp
}
