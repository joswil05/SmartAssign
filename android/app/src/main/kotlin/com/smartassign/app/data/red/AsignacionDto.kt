package com.smartassign.app.data.red

import kotlinx.serialization.Serializable

/**
 * Formas exactas de `backend/SmartAssign.Api/Endpoints/AsignacionEndpoints.cs`
 * (E6.7/E6.8) — `sp_SugerirPuesto` (00 §8.5) y `sp_AsignarPersona` (00 §B1).
 */

/** `SugerenciaRespuesta` — exactamente uno de (puestoId,nivel) o (codigoRechazo,mensaje) viene poblado. */
@Serializable
data class SugerenciaResponse(
    val puestoId: Int? = null,
    val nivel: Int? = null,
    val codigoRechazo: String? = null,
    val mensaje: String? = null
)

@Serializable
data class AsignarPeticionRequest(
    val personalId: Int,
    val idempotencyKey: String,
    val cederPerfil: Boolean = false
)

@Serializable
data class AsignarResponse(val asignacionId: Long)

/** Cuerpo del 409 de `POST /api/puestos/{id}/asignar` — el rechazo nominal de B1. */
@Serializable
data class RechazoAsignacionResponse(val codigo: String, val mensaje: String)
