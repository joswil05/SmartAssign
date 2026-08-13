package com.smartassign.app.data.sesion

import com.smartassign.app.data.conectividad.ConexionTiempoReal
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
    @ClienteCrudo private val clienteCrudo: OkHttpClient,
    private val purgaCache: PurgaCacheLocal,
    private val conexionTiempoReal: ConexionTiempoReal
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
                manejarRespuestaSesion(respuesta).tambienConectarSiHuboExito()
            } catch (_: IOException) {
                ResultadoAuth.SinConexion
            }
        }

    override suspend fun renovarSesion(): ResultadoAuth = withContext(Dispatchers.IO) {
        val refresh = local.tokens()?.refreshToken ?: return@withContext ResultadoAuth.Rechazo("REFRESH_INVALIDO")
        try {
            val respuesta = api.refresh(RefreshRequest(refresh, local.deviceId()))
            manejarRespuestaSesion(respuesta, conservarRefreshPrevio = true).tambienConectarSiHuboExito()
        } catch (_: IOException) {
            ResultadoAuth.SinConexion
        }
    }

    override suspend fun reentrarConPin(pin: String): ResultadoAuth = withContext(Dispatchers.IO) {
        val usuarioId = local.identidad()?.usuarioId
            ?: return@withContext ResultadoAuth.Rechazo("PIN_NO_CONFIGURADO")
        try {
            val respuesta = api.pin(PinRequest(usuarioId, pin, local.deviceId()))
            manejarRespuestaSesion(respuesta, conservarRefreshPrevio = true).tambienConectarSiHuboExito()
        } catch (_: IOException) {
            ResultadoAuth.SinConexion
        }
    }

    /**
     * UT-E13.5: arranca `PlantaHub` en cuanto hay tokens de verdad — no
     * hace falta esperar al `ConectividadTicker` (hasta 15 s de margen)
     * para la primera conexión de la sesión. Un fallo de conexión aquí
     * no vuelve a fallar el login: `ConexionTiempoReal.conectar()` ya es
     * silenciosa por diseño (`PlantaHubConectividad`), y el ticker
     * reintenta solo.
     */
    private suspend fun ResultadoAuth.tambienConectarSiHuboExito(): ResultadoAuth = also {
        if (it is ResultadoAuth.Ok) conexionTiempoReal.conectar()
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
        // E13.2 / 00 §D3: la caché cifrada se purga TAMBIÉN, no solo las
        // preferencias. El teléfono es compartido por línea (D6): sin
        // esto, el siguiente usuario heredaba los datos médicos del
        // anterior. Va después de `limpiarSesion()` a propósito — si algo
        // fallara aquí, la sesión ya quedó cerrada de todas formas.
        purgaCache.purgar()
        // E13.5: sin esto, PlantaHub seguiría con el JWT de la sesión que
        // se acaba de cerrar — el mismo teléfono compartido (D6) al que
        // se le purgó la caché no puede quedarse con el canal en vivo
        // abierto a nombre de otra persona.
        conexionTiempoReal.desconectar()
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
