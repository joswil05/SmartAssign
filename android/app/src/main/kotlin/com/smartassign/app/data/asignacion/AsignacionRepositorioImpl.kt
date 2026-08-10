package com.smartassign.app.data.asignacion

import com.smartassign.app.data.red.AsignacionApi
import com.smartassign.app.data.red.AsignarPeticionRequest
import com.smartassign.app.data.red.RechazoAsignacionResponse
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import retrofit2.Response
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AsignacionRepositorioImpl @Inject constructor(
    private val api: AsignacionApi,
    private val json: Json
) : AsignacionRepositorio {

    override suspend fun sugerirPuesto(personalId: Int): ResultadoSugerencia = withContext(Dispatchers.IO) {
        try {
            val cuerpo = api.sugerencia(personalId).body()
            when {
                cuerpo == null -> ResultadoSugerencia.SinConexion
                cuerpo.codigoRechazo != null -> ResultadoSugerencia.SinSugerencia(cuerpo.codigoRechazo, cuerpo.mensaje ?: "")
                cuerpo.puestoId != null -> ResultadoSugerencia.Ok(cuerpo.puestoId, cuerpo.nivel ?: 0)
                else -> ResultadoSugerencia.SinConexion // forma inesperada — ni sugerencia ni rechazo
            }
        } catch (_: IOException) {
            ResultadoSugerencia.SinConexion
        }
    }

    override suspend fun asignarPersona(
        puestoId: Int,
        personalId: Int,
        idempotencyKey: String,
        cederPerfil: Boolean
    ): ResultadoAsignar = withContext(Dispatchers.IO) {
        try {
            val respuesta = api.asignar(puestoId, AsignarPeticionRequest(personalId, idempotencyKey, cederPerfil))
            when {
                respuesta.code() == 409 -> {
                    val rechazo = rechazoDesde(respuesta)
                    ResultadoAsignar.Rechazado(rechazo.codigo, rechazo.mensaje)
                }
                respuesta.isSuccessful -> respuesta.body()?.let { ResultadoAsignar.Ok(it.asignacionId) } ?: ResultadoAsignar.SinConexion
                else -> ResultadoAsignar.SinConexion
            }
        } catch (_: IOException) {
            ResultadoAsignar.SinConexion
        }
    }

    private fun rechazoDesde(respuesta: Response<*>): RechazoAsignacionResponse {
        val cuerpo = respuesta.errorBody()?.string()
        return cuerpo?.let {
            runCatching { json.decodeFromString(RechazoAsignacionResponse.serializer(), it) }.getOrNull()
        } ?: RechazoAsignacionResponse(codigo = "DESCONOCIDO", mensaje = "")
    }
}
