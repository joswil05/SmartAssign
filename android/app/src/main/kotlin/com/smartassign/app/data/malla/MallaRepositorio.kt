package com.smartassign.app.data.malla

/** Espejo cliente de `LineaEndpoints.PuestoMallaRespuesta` — 03 §3.1, la tarjeta de puesto. */
data class OcupantePuesto(val personalId: Int, val nombreCompleto: String, val ficha: String, val categoria: String)

data class PuestoMalla(
    val id: Int,
    val codigo: String,
    val nombrePuesto: String,
    val tipo: String,
    val situacion: String,
    val ocupante: OcupantePuesto?,
    val indicadorMedico: Int,
    val microCopia: String
)

sealed interface ResultadoMalla {
    data class Ok(val puestos: List<PuestoMalla>) : ResultadoMalla
    data object SinAlcance : ResultadoMalla // 403 — no debería pasar si la línea viene de /auth/me, pero honesto ante el caso borde
    data object SinConexion : ResultadoMalla
}

interface MallaRepositorio {
    suspend fun puestosDeLinea(lineaId: Int): ResultadoMalla
}
