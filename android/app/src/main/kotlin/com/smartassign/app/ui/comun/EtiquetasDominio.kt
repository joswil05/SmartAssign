package com.smartassign.app.ui.comun

/**
 * Vocabulario de presentación compartido — mismo criterio que
 * `fn_SituacionPuesto` (E6.4): una sola fuente para no arriesgarse a que
 * dos pantallas etiqueten la misma categoría distinto. Usado por
 * `TarjetaDePuesto` (03 §3.1, E6.4) y `ModalConfirmacionIdentidad`
 * (03 §3.3, E6.6).
 */
fun etiquetaCategoria(categoria: String) = when (categoria) {
    "operario" -> "Operario"
    "operador_a" -> "Operador A"
    "operador_b" -> "Operador B"
    "operador_c" -> "Operador C"
    "averiero" -> "Averiero"
    "liderazgo" -> "Liderazgo"
    else -> categoria
}
