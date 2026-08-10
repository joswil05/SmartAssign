package com.smartassign.app.data.sesion

import com.smartassign.app.data.red.LineaVigenteResponse

/** Resultado de cualquier operación del ciclo de sesión — espejo cliente de `ResultadoLogin` (backend). */
sealed interface ResultadoAuth {
    data class Ok(val usuarioId: Int, val rol: String, val nombre: String) : ResultadoAuth
    data class Rechazo(val codigo: String) : ResultadoAuth
    /** Sin red o el servidor no respondió — distinto de un rechazo real (§12.4: no se inventa una causa). */
    data object SinConexion : ResultadoAuth
}

/** Quién soy y cuál es mi línea vigente — siempre resuelto en vivo (§2.3), nunca cacheado. */
data class QuienSoy(val usuarioId: Int, val rol: String, val nombre: String, val linea: LineaVigenteResponse?)

/**
 * Orquesta [com.smartassign.app.data.red.AuthApi] + [SesionLocal] — el
 * mapa de navegación 02 §1.0/§1.1 depende solo de esta interfaz, nunca
 * de Retrofit ni de las preferencias cifradas directamente.
 */
interface SesionRepositorio {
    suspend fun verificarServidor(url: String): Boolean
    fun guardarServidor(url: String)
    fun servidorConfigurado(): Boolean

    /** Hay un refresh token guardado y, según el reloj local, no debería haber expirado. Es una pista, no una garantía — el servidor decide de verdad al refrescar. */
    fun haySesionGuardada(): Boolean

    suspend fun iniciarSesion(username: String, password: String): ResultadoAuth
    suspend fun renovarSesion(): ResultadoAuth
    suspend fun reentrarConPin(pin: String): ResultadoAuth
    suspend fun quienSoy(): QuienSoy?
    suspend fun cerrarSesion()

    fun identidadGuardada(): IdentidadGuardada?
}
