package com.smartassign.app.ui.navegacion

import com.smartassign.app.data.red.LineaVigenteResponse

/** Nombres de destino del `NavHost` — 02 §1.1/§1.2/§1.3. */
object Rutas {
    const val ARRANQUE = "arranque"
    const val ALTA_DISPOSITIVO = "alta_dispositivo"
    const val LOGIN = "login"
    const val PIN = "pin"
    const val PANEL_PLANTA = "panel_planta"       // Coordinador — E6 más adelante
    const val MALLA_LINEA = "malla_linea"         // Supervisor de línea — E6.4
    const val PANEL_BOLSON = "panel_bolson"       // Supervisor de L8 — C7, raíz distinta
    const val SIN_LINEA = "sin_linea"             // Terminal — §2.3, nunca se elige de una lista
}

/**
 * <¿Qué rol tiene?> → <¿Tiene línea asignada?> → <¿Su línea es la L8?>
 * (02 §1.1, último tramo del mapa). Función pura para poder probar las
 * cuatro ramas sin Compose ni red — la resuelve quien llama, con lo que
 * ya devolvió `GET /api/auth/me`.
 */
fun destinoTrasAutenticar(rol: String, linea: LineaVigenteResponse?): String = when {
    rol == "coordinador" -> Rutas.PANEL_PLANTA
    linea == null -> Rutas.SIN_LINEA
    linea.esBolson -> Rutas.PANEL_BOLSON
    else -> Rutas.MALLA_LINEA
}
