package com.smartassign.app.data.asignacion

class FakeAsignacionRepositorio : AsignacionRepositorio {
    var resultadoSugerencia: ResultadoSugerencia = ResultadoSugerencia.SinConexion
    var resultadoAsignar: ResultadoAsignar = ResultadoAsignar.SinConexion
    var ultimoPersonalIdSugerido: Int? = null
    var ultimaPeticionAsignar: PeticionAsignarRegistrada? = null

    data class PeticionAsignarRegistrada(
        val puestoId: Int,
        val personalId: Int,
        val idempotencyKey: String,
        val cederPerfil: Boolean
    )

    override suspend fun sugerirPuesto(personalId: Int): ResultadoSugerencia {
        ultimoPersonalIdSugerido = personalId
        return resultadoSugerencia
    }

    override suspend fun asignarPersona(
        puestoId: Int,
        personalId: Int,
        idempotencyKey: String,
        cederPerfil: Boolean
    ): ResultadoAsignar {
        ultimaPeticionAsignar = PeticionAsignarRegistrada(puestoId, personalId, idempotencyKey, cederPerfil)
        return resultadoAsignar
    }
}
