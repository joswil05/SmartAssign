package com.smartassign.app.data.sesion

import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.ClienteCrudo
import com.smartassign.app.data.red.ErrorSesion
import com.smartassign.app.data.red.LoginRequest
import com.smartassign.app.data.red.PinRequest
import com.smartassign.app.data.red.RefreshRequest
import com.smartassign.app.data.red.LogoutRequest
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import kotlinx.serialization.json.Json
import okhttp3.OkHttpClient
import okhttp3.Request
import retrofit2.Response
import java.io.IOException
import java.time.Instant
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SesionRepositorioImpl @Inject constructor(
    private val api: AuthApi,
    private val local: SesionLocal,
    private val json: Json,
    @ClienteCrudo private val clienteCrudo: OkHttpClient
) : SesionRepositorio {

    override suspend fun verificarServidor(url: String): Boolean = withContext(Dispatchers.IO) {
        try {
            val base = normalizarUrl(url)
            val peticion = Request.Builder().url("${base}api/servidor/info").build()
            clienteCrudo.newCall(peticion).execute().use { it.isSuccessful }
        } catch (_: IOException) {
            false
        } catch (_: IllegalArgumentException) {
            false // URL mal formada — el QR no traía lo esperado
        }
    }

    override fun guardarServidor(url: String) = local.guardarServidorUrl(normalizarUrl(url))

    override fun servidorConfigurado(): Boolean = local.servidorUrl() != null

    override fun haySesionGuardada(): Boolean {
        val tokens = local.tokens() ?: return false
        val refresh = tokens.refreshToken ?: return false
        val expira = tokens.refreshExpiraEn ?: return true // sin fecha conocida: se deja que el servidor decida
        return refresh.isNotBlank() && expira.isAfter(Instant.now())
    }

    override suspend fun iniciarSesion(username: String, password: String): ResultadoAuth =
        withContext(Dispatchers.IO) {
            try {
                val respuesta = api.login(LoginRequest(username, password, local.deviceId()))
                manejarRespuestaSesion(respuesta)
            } catch (_: IOException) {
                ResultadoAuth.SinConexion
            }
        }

    override suspend fun renovarSesion(): ResultadoAuth = withContext(Dispatchers.IO) {
        val refresh = local.tokens()?.refreshToken ?: return@withContext ResultadoAuth.Rechazo("REFRESH_INVALIDO")
        try {
            val respuesta = api.refresh(RefreshRequest(refresh, local.deviceId()))
            manejarRespuestaSesion(respuesta, conservarRefreshPrevio = true)
        } catch (_: IOException) {
            ResultadoAuth.SinConexion
        }
    }

    override suspend fun reentrarConPin(pin: String): ResultadoAuth = withContext(Dispatchers.IO) {
        val usuarioId = local.identidad()?.usuarioId
            ?: return@withContext ResultadoAuth.Rechazo("PIN_NO_CONFIGURADO")
        try {
            val respuesta = api.pin(PinRequest(usuarioId, pin, local.deviceId()))
            manejarRespuestaSesion(respuesta, conservarRefreshPrevio = true)
        } catch (_: IOException) {
            ResultadoAuth.SinConexion
        }
    }

    override suspend fun quienSoy(): QuienSoy? = withContext(Dispatchers.IO) {
        try {
            val respuesta = api.me()
            if (!respuesta.isSuccessful) return@withContext null
            val cuerpo = respuesta.body() ?: return@withContext null
            QuienSoy(cuerpo.usuarioId, cuerpo.rol, cuerpo.nombre, cuerpo.linea)
        } catch (_: IOException) {
            null
        }
    }

    override suspend fun cerrarSesion() {
        withContext(Dispatchers.IO) {
            try {
                api.logout(LogoutRequest(local.deviceId()))
            } catch (_: IOException) {
                // Mejor esfuerzo — el usuario sale de todas formas (D3: la purga local es lo que de verdad importa aquí).
            }
        }
        local.limpiarSesion()
    }

    override fun identidadGuardada(): IdentidadGuardada? = local.identidad()

    private fun manejarRespuestaSesion(
        respuesta: Response<com.smartassign.app.data.red.SesionResponse>,
        conservarRefreshPrevio: Boolean = false
    ): ResultadoAuth {
        if (!respuesta.isSuccessful) return ResultadoAuth.Rechazo(codigoDeError(respuesta))

        val cuerpo = respuesta.body() ?: return ResultadoAuth.Rechazo("RESPUESTA_VACIA")
        val refreshPrevio = local.tokens()

        local.guardarTokens(
            TokensGuardados(
                accessToken = cuerpo.accessToken,
                accessExpiraEn = Instant.parse(cuerpo.accessExpiraEn),
                refreshToken = cuerpo.refreshToken ?: refreshPrevio?.refreshToken.takeIf { conservarRefreshPrevio },
                refreshExpiraEn = cuerpo.refreshExpiraEn?.let(Instant::parse)
                    ?: refreshPrevio?.refreshExpiraEn.takeIf { conservarRefreshPrevio }
            )
        )
        local.guardarIdentidad(IdentidadGuardada(cuerpo.usuarioId, cuerpo.rol, cuerpo.nombre))

        return ResultadoAuth.Ok(cuerpo.usuarioId, cuerpo.rol, cuerpo.nombre)
    }

    private fun codigoDeError(respuesta: Response<*>): String {
        val cuerpo = respuesta.errorBody()?.string()
        return cuerpo?.let {
            runCatching { json.decodeFromString(ErrorSesion.serializer(), it).codigo }.getOrNull()
        } ?: "DESCONOCIDO"
    }

    private fun normalizarUrl(url: String): String {
        val recortada = url.trim()
        require(recortada.startsWith("http://") || recortada.startsWith("https://")) { "URL sin esquema" }
        return if (recortada.endsWith("/")) recortada else "$recortada/"
    }
}
