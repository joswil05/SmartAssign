package com.smartassign.app.data.red

import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST

/**
 * El ciclo de sesión de D6 / 04 §6.4, tal como lo expone
 * `backend/SmartAssign.Api/Endpoints/AuthEndpoints.cs`. Sin `/api/v1` —
 * la Api real de esta rama todavía no versiona sus rutas (desviación ya
 * documentada en la etapa E2, no una decisión nueva de esta UT).
 */
interface AuthApi {
    @POST("api/auth/login")
    suspend fun login(@Body cuerpo: LoginRequest): Response<SesionResponse>

    @POST("api/auth/refresh")
    suspend fun refresh(@Body cuerpo: RefreshRequest): Response<SesionResponse>

    @POST("api/auth/pin")
    suspend fun pin(@Body cuerpo: PinRequest): Response<SesionResponse>

    @POST("api/auth/logout")
    suspend fun logout(@Body cuerpo: LogoutRequest): Response<Unit>

    @GET("api/auth/me")
    suspend fun me(): Response<MeResponse>

    @GET("api/servidor/info")
    suspend fun servidorInfo(): Response<ServidorInfoResponse>
}
