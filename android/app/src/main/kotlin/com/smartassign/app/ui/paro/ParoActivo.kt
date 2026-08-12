package com.smartassign.app.ui.paro

import java.time.Instant

/**
 * §11.1: el cronómetro solo conoce dos estados — hay un paro abierto en la
 * línea vigente, o no lo hay. No existe "pausado" ni "varios a la vez":
 * `UX_Paro_abierto` (E11.1) ya garantiza como mucho un paro sin `fin` por
 * `jornada_linea_id`, así que el cliente no necesita modelar más que esto.
 *
 * `categoria` llega tal cual la devuelve el servidor (`CategoriaParo.Nombre`,
 * p. ej. "Mecánico") — el cliente no traduce códigos aquí, solo aplica
 * mayúsculas para el rótulo (03 §3.8: "PARO · MECÁNICO").
 */
data class ParoActivo(
    val categoria: String,
    val inicio: Instant
)
