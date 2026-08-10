package com.smartassign.app.data.red

import kotlinx.serialization.Serializable

/** Forma exacta de `LineaEndpoints.PuestoMallaRespuesta` (backend) — 03 §3.1. */
@Serializable
data class OcupanteResponse(
    val personalId: Int,
    val nombreCompleto: String,
    val ficha: String,
    val categoria: String
)

@Serializable
data class PuestoMallaResponse(
    val id: Int,
    val codigo: String,
    val nombrePuesto: String,
    val tipo: String,
    val situacion: String,
    val ocupante: OcupanteResponse? = null,
    val indicadorMedico: Int,
    val microCopia: String
)
