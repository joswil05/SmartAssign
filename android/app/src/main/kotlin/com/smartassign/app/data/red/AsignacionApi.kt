package com.smartassign.app.data.red

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

/**
 * `AsignacionEndpoints.cs` — sugerencia (`sp_SugerirPuesto`, E6.7) y
 * asignación (`sp_AsignarPersona`, E6.8). El alcance de línea del
 * supervisor ya lo resuelve el servidor (§2.2); este cliente nunca lo
 * envía.
 */
interface AsignacionApi {
    @GET("api/asignaciones/sugerencia")
    suspend fun sugerencia(@Query("personalId") personalId: Int): Response<SugerenciaResponse>

    @POST("api/puestos/{id}/asignar")
    suspend fun asignar(@Path("id") puestoId: Int, @Body cuerpo: AsignarPeticionRequest): Response<AsignarResponse>
}
