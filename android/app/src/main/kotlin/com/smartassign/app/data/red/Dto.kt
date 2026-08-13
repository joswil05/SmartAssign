package com.smartassign.app.data.red

import kotlinx.serialization.Serializable

/**
 * Formas exactas de `backend/SmartAssign.Api/Endpoints/AuthEndpoints.cs` y
 * `ServidorEndpoints.cs` — no una interpretación de 05_TRD.md §2.3, sino
 * lo que la Api real de esta misma rama ya devuelve.
 */

@Serializable
data class LoginRequest(val username: String, val password: String, val deviceId: String)

@Serializable
data class RefreshRequest(val refreshToken: String, val deviceId: String)

@Serializable
data class PinRequest(val usuarioId: Int, val pin: String, val deviceId: String)

@Serializable
data class LogoutRequest(val deviceId: String)

@Serializable
data class SesionResponse(
    val usuarioId: Int,
    val rol: String,
    val nombre: String,
    val accessToken: String,
    val accessExpiraEn: String,
    val refreshToken: String? = null,
    val refreshExpiraEn: String? = null
)

/** Cuerpo de error de `AuthEndpoints.AResultadoHttp` — nunca texto libre, siempre un código estable. */
@Serializable
data class ErrorSesion(val codigo: String)

@Serializable
data class LineaVigenteResponse(val id: Int, val codigo: String, val nombre: String, val esBolson: Boolean)

@Serializable
data class MeResponse(
    val usuarioId: Int,
    val rol: String,
    val nombre: String,
    val linea: LineaVigenteResponse? = null
)

@Serializable
data class ServidorInfoResponse(val servidor: String)

/** Forma exacta de `VersionAppEndpoints.VersionActualRespuesta` (E14.6, 00 §F3, 04 §10.1). */
@Serializable
data class VersionActualResponse(
    val versionNombre: String,
    val versionCodigo: Int,
    val versionMinimaApi: Int,
    val notas: String? = null,
    val publicadaEn: String
)
