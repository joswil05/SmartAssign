package com.smartassign.app.data.red

import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Path

/** `LineaEndpoints.cs` — alcance ya resuelto por `AlcanceLineaEndpointFilter` en el servidor (§2.3). */
interface MallaApi {
    @GET("api/lineas/{lineaId}/puestos")
    suspend fun puestosDeLinea(@Path("lineaId") lineaId: Int): Response<List<PuestoMallaResponse>>
}
