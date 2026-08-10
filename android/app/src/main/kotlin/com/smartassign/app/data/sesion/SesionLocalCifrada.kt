package com.smartassign.app.data.sesion

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey
import dagger.hilt.android.qualifiers.ApplicationContext
import java.time.Instant
import javax.inject.Inject
import javax.inject.Singleton

private const val ARCHIVO = "smartassign_sesion"
private const val CLAVE_DEVICE_ID = "device_id"
private const val CLAVE_SERVIDOR_URL = "servidor_url"
private const val CLAVE_ACCESS_TOKEN = "access_token"
private const val CLAVE_ACCESS_EXPIRA = "access_expira_en"
private const val CLAVE_REFRESH_TOKEN = "refresh_token"
private const val CLAVE_REFRESH_EXPIRA = "refresh_expira_en"
private const val CLAVE_USUARIO_ID = "usuario_id"
private const val CLAVE_ROL = "rol"
private const val CLAVE_NOMBRE = "nombre"

// EncryptedSharedPreferences/MasterKey quedaron "deprecated" en
// security-crypto 1.1.0 a favor de un reemplazo basado en DataStore+Tink
// que Google todavía no estabiliza — siguen siendo la vía soportada
// mientras tanto. Migrar antes de que exista un reemplazo estable
// inventaría una dependencia que no corresponde a esta etapa.
@Suppress("DEPRECATION")
@Singleton
class SesionLocalCifrada @Inject constructor(@ApplicationContext contexto: Context) : SesionLocal {

    private val prefs: SharedPreferences by lazy {
        val maestra = MasterKey.Builder(contexto).setKeyScheme(MasterKey.KeyScheme.AES256_GCM).build()
        EncryptedSharedPreferences.create(
            contexto, ARCHIVO, maestra,
            EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
            EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM
        )
    }

    override fun deviceId(): String =
        prefs.getString(CLAVE_DEVICE_ID, null) ?: nuevoDeviceId().also {
            prefs.edit().putString(CLAVE_DEVICE_ID, it).apply()
        }

    override fun servidorUrl(): String? = prefs.getString(CLAVE_SERVIDOR_URL, null)

    override fun guardarServidorUrl(url: String) {
        prefs.edit().putString(CLAVE_SERVIDOR_URL, url).apply()
    }

    override fun tokens(): TokensGuardados? {
        val access = prefs.getString(CLAVE_ACCESS_TOKEN, null) ?: return null
        val accessExpira = prefs.getString(CLAVE_ACCESS_EXPIRA, null) ?: return null
        return TokensGuardados(
            accessToken = access,
            accessExpiraEn = Instant.parse(accessExpira),
            refreshToken = prefs.getString(CLAVE_REFRESH_TOKEN, null),
            refreshExpiraEn = prefs.getString(CLAVE_REFRESH_EXPIRA, null)?.let(Instant::parse)
        )
    }

    override fun guardarTokens(tokens: TokensGuardados) {
        prefs.edit()
            .putString(CLAVE_ACCESS_TOKEN, tokens.accessToken)
            .putString(CLAVE_ACCESS_EXPIRA, tokens.accessExpiraEn.toString())
            .apply {
                if (tokens.refreshToken != null) putString(CLAVE_REFRESH_TOKEN, tokens.refreshToken)
                if (tokens.refreshExpiraEn != null) putString(CLAVE_REFRESH_EXPIRA, tokens.refreshExpiraEn.toString())
            }
            .apply()
    }

    override fun identidad(): IdentidadGuardada? {
        val usuarioId = prefs.getInt(CLAVE_USUARIO_ID, -1).takeIf { it != -1 } ?: return null
        val rol = prefs.getString(CLAVE_ROL, null) ?: return null
        val nombre = prefs.getString(CLAVE_NOMBRE, null) ?: return null
        return IdentidadGuardada(usuarioId, rol, nombre)
    }

    override fun guardarIdentidad(identidad: IdentidadGuardada) {
        prefs.edit()
            .putInt(CLAVE_USUARIO_ID, identidad.usuarioId)
            .putString(CLAVE_ROL, identidad.rol)
            .putString(CLAVE_NOMBRE, identidad.nombre)
            .apply()
    }

    override fun limpiarSesion() {
        prefs.edit()
            .remove(CLAVE_ACCESS_TOKEN).remove(CLAVE_ACCESS_EXPIRA)
            .remove(CLAVE_REFRESH_TOKEN).remove(CLAVE_REFRESH_EXPIRA)
            .remove(CLAVE_USUARIO_ID).remove(CLAVE_ROL).remove(CLAVE_NOMBRE)
            .apply()
    }
}
