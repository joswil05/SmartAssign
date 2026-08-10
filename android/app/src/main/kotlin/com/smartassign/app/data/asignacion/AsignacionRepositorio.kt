package com.smartassign.app.data.asignacion

/** Resultado de `sp_SugerirPuesto` (E6.7) — 00 §8.5, la escalera de 4 niveles. */
sealed interface ResultadoSugerencia {
    data class Ok(val puestoId: Int, val nivel: Int) : ResultadoSugerencia

    /** El servidor no encontró candidato — código y mensaje reales de `sp_SugerirPuesto`, nunca reescritos aquí. */
    data class SinSugerencia(val codigo: String, val mensaje: String) : ResultadoSugerencia
    data object SinConexion : ResultadoSugerencia
}

/** Resultado de `sp_AsignarPersona` (E6.8) — 00 §B1, bloqueo determinista + idempotencia. */
sealed interface ResultadoAsignar {
    data class Ok(val asignacionId: Long) : ResultadoAsignar

    /** Rechazo nominal real del servidor (p. ej. "Fulano acaba de ser registrado..."), nunca reescrito aquí. */
    data class Rechazado(val codigo: String, val mensaje: String) : ResultadoAsignar
    data object SinConexion : ResultadoAsignar
}

interface AsignacionRepositorio {
    suspend fun sugerirPuesto(personalId: Int): ResultadoSugerencia

    /**
     * `idempotencyKey`: la genera quien llama, una sola vez por intento de
     * confirmación (00 §B1) — reenviar la misma clave hace que el servidor
     * devuelva el resultado ya resuelto en vez de competir de nuevo por el
     * puesto.
     */
    suspend fun asignarPersona(
        puestoId: Int,
        personalId: Int,
        idempotencyKey: String,
        cederPerfil: Boolean = false
    ): ResultadoAsignar
}
