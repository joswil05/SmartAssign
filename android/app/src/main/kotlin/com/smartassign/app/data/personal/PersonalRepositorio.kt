package com.smartassign.app.data.personal

/** Espejo cliente de `PersonalEndpoints.PersonalPorFichaRespuesta` — 03 §3.3, el modal de confirmación de identidad. */
data class PersonaConfirmacion(
    val personalId: Int,
    val nombreCompleto: String,
    val ficha: String,
    val categoria: String,
    val restriccionesMedicas: List<String>
)

sealed interface ResultadoPersonal {
    data class Ok(val persona: PersonaConfirmacion) : ResultadoPersonal
    data object NoEncontrado : ResultadoPersonal // 404 — la ficha no corresponde a nadie del padrón
    data object SinConexion : ResultadoPersonal
}

interface PersonalRepositorio {
    suspend fun porFicha(ficha: String): ResultadoPersonal
}
