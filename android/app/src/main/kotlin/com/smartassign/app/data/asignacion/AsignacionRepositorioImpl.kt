package com.smartassign.app.data.asignacion

import com.smartassign.app.data.conectividad.ConectividadRepositorio
import com.smartassign.app.data.conectividad.EstadoConectividad
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

/**
 * `asignarPersona` es, hoy, el único camino real de escritura que existe
 * en el cliente Android (`sp_AsignarPersona`, E6.8) — mover personal
 * entre líneas (la otra mitad literal de §12.1) todavía no tiene
 * pantalla propia (`ui/destinos` sigue siendo un placeholder). Por eso el
 * bloqueo de E13.3 se demuestra aquí, y no en un lugar que no existe
 * todavía — mismo criterio de alcance que E12.3 demostró la bandeja de
 * salida con un solo productor real en vez de cablear los ~8 restantes.
 */
@Singleton
class AsignacionRepositorioImpl @Inject constructor(
    private val api: AsignacionApi,
    private val json: Json,
    private val conectividad: ConectividadRepositorio
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
        // §12.1, literal: "se bloquea el registro de nuevas asignaciones"
        // — no "se intenta y se falla". Sin esto, cada intento offline
        // esperaría el timeout de red entero para enterarse de lo mismo
        // que ya se sabe. Y es la mitad que hace real "no se encola
        // nada" (05 §4.3): ni siquiera se construye la petición.
        if (conectividad.estado.value == EstadoConectividad.SinConexion) {
            return@withContext ResultadoAsignar.SinConexion
        }
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
