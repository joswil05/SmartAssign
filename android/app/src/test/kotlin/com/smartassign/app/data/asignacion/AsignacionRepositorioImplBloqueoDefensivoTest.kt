package com.smartassign.app.data.asignacion

import com.smartassign.app.data.conectividad.ConectividadRepositorioImpl
import com.smartassign.app.data.red.AsignacionApi
import com.smartassign.app.data.red.AsignarPeticionRequest
import com.smartassign.app.data.red.AsignarResponse
import com.smartassign.app.data.red.SugerenciaResponse
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response

/**
 * UT-E13.3 (docs/PROGRESO.md): "Bloqueo defensivo · no se encola nada"
 * (§12.1, 05 §4.3), demostrado en `sp_AsignarPersona` — el único camino
 * de escritura real que existe hoy en el cliente Android.
 *
 * La garantía que da nombre a la UT se prueba con una cuenta de
 * invocaciones sobre un `AsignacionApi` falso: si "sin conexión" de
 * verdad bloquea, ese contador debe quedar en CERO — no hay ninguna
 * petición que reintentar más tarde porque nunca se construyó.
 */
class AsignacionRepositorioImplBloqueoDefensivoTest {

    private class ApiDeConteo : AsignacionApi {
        var vecesLlamadaSugerencia = 0
        var vecesLlamadaAsignar = 0

        override suspend fun sugerencia(personalId: Int): Response<SugerenciaResponse> {
            vecesLlamadaSugerencia++
            return Response.success(SugerenciaResponse(puestoId = 1, nivel = 1, codigoRechazo = null, mensaje = null))
        }

        override suspend fun asignar(puestoId: Int, cuerpo: AsignarPeticionRequest): Response<AsignarResponse> {
            vecesLlamadaAsignar++
            return Response.success(AsignarResponse(asignacionId = 999L))
        }
    }

    private fun nuevoRepositorio(api: ApiDeConteo, conectada: Boolean): AsignacionRepositorioImpl {
        val conectividad = ConectividadRepositorioImpl()
        if (!conectada) conectividad.reportarInalcanzable()
        return AsignacionRepositorioImpl(api, Json { ignoreUnknownKeys = true }, conectividad)
    }

    @Test
    fun sin_conexion_asignarPersona_no_llama_a_la_api_ni_una_vez() = runTest {
        val api = ApiDeConteo()
        val repo = nuevoRepositorio(api, conectada = false)

        val resultado = repo.asignarPersona(puestoId = 1, personalId = 1, idempotencyKey = "k1")

        assertEquals(ResultadoAsignar.SinConexion, resultado)
        // La prueba de que "no se encola nada": si hubiera una cola, este
        // contador sería 1 (el intento diferido) tarde o temprano.
        assertEquals(0, api.vecesLlamadaAsignar)
    }

    @Test
    fun conectado_asignarPersona_si_llama_a_la_api_y_confirma() = runTest {
        val api = ApiDeConteo()
        val repo = nuevoRepositorio(api, conectada = true)

        val resultado = repo.asignarPersona(puestoId = 1, personalId = 1, idempotencyKey = "k1")

        assertEquals(ResultadoAsignar.Ok(999L), resultado)
        assertEquals(1, api.vecesLlamadaAsignar)
    }

    @Test
    fun sin_conexion_el_bloqueo_es_inmediato_no_espera_ningun_timeout_de_red() = runTest {
        // §12.1: "se bloquea", no "se intenta y tarda en fallar". Esta
        // prueba no mide reloj —correría igual con o sin el arreglo—
        // pero documenta la intención: al no tocar la red, no hay
        // ningún timeout que esperar. `vecesLlamadaAsignar == 0` (arriba)
        // es la prueba real; esta deja explícito el porqué.
        val api = ApiDeConteo()
        val repo = nuevoRepositorio(api, conectada = false)

        repeat(3) { repo.asignarPersona(puestoId = 1, personalId = 1, idempotencyKey = "k$it") }

        assertEquals(0, api.vecesLlamadaAsignar)
    }

    @Test
    fun sugerirPuesto_no_esta_bloqueado_por_conectividad_no_es_un_registro() = runTest {
        // §12.1 bloquea "movimiento entre líneas" y "nuevas asignaciones"
        // — sugerirPuesto no escribe nada (es sp_SugerirPuesto, una
        // consulta), así que sigue su camino reactivo de siempre.
        val api = ApiDeConteo()
        val repo = nuevoRepositorio(api, conectada = false)

        repo.sugerirPuesto(personalId = 1)

        assertEquals(1, api.vecesLlamadaSugerencia)
    }
}
