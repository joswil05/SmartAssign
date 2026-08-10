package com.smartassign.app.data.red

import kotlinx.serialization.Serializable

/** Forma exacta de `PersonalEndpoints.PersonalPorFichaRespuesta` (backend) — 03 §3.3. */
@Serializable
data class PersonalPorFichaResponse(
    val personalId: Int,
    val nombreCompleto: String,
    val ficha: String,
    val categoria: String,
    val restriccionesMedicas: List<String>
)
