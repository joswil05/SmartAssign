package com.smartassign.app.data.sesion

import java.time.Instant
import java.util.UUID

/** Lo poco que persiste EN EL DISPOSITIVO — nunca la línea (§2.3, D6). */
data class TokensGuardados(
    val accessToken: String,
    val accessExpiraEn: Instant,
    val refreshToken: String?,
    val refreshExpiraEn: Instant?
)

data class IdentidadGuardada(val usuarioId: Int, val rol: String, val nombre: String)

/**
 * Almacenamiento cifrado del dispositivo — `device_id`, URL del servidor
 * de planta (F3) y el ciclo de sesión (D6, 04 §6.4). Cifrado con
 * `EncryptedSharedPreferences` (Keystore de Android): son credenciales de
 * sesión, no datos de negocio — el caché cifrado con SQLCipher para datos
 * de negocio es de la etapa E13 (D3), un problema distinto.
 *
 * La línea del supervisor **nunca** vive aquí — se resuelve en vivo en
 * cada petición contra `/api/auth/me` (§2.3).
 */
interface SesionLocal {
    /** Identificador estable de este teléfono — generado una sola vez, nunca reinstalado sin perderlo. */
    fun deviceId(): String

    fun servidorUrl(): String?
    fun guardarServidorUrl(url: String)

    fun tokens(): TokensGuardados?
    fun guardarTokens(tokens: TokensGuardados)

    fun identidad(): IdentidadGuardada?
    fun guardarIdentidad(identidad: IdentidadGuardada)

    /** Logout (D6): borra tokens + identidad. Conserva `servidorUrl` y `deviceId` — el teléfono sigue siendo el mismo. */
    fun limpiarSesion()
}

/** Fábrica de un `device_id` nuevo — solo la usa la implementación real; separada para poder probarla sin Android Keystore. */
fun nuevoDeviceId(): String = UUID.randomUUID().toString()
