package com.smartassign.app.data.red

import retrofit2.Response
import retrofit2.http.GET
import retrofit2.http.Path

/** `PersonalEndpoints.cs` — resolución del escaneo de gafete (00 §E1, §12.2). Sin alcance de línea, ver el comentario del propio endpoint. */
interface PersonalApi {
    @GET("api/personal/por-ficha/{ficha}")
    suspend fun porFicha(@Path("ficha") ficha: String): Response<PersonalPorFichaResponse>
}
