package com.smartassign.app.data.conectividad

import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HubConnectionState
import com.smartassign.app.data.sesion.SesionLocal
import io.reactivex.rxjava3.core.Single
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import javax.inject.Inject
import javax.inject.Singleton

/**
 * UT-E13.5 (05 §4.3): conecta a `PlantaHub` (`/hub/planta`, E12.1) por la
 * MISMA razón por la que el servidor lo construyó sin ningún método
 * invocable — aquí tampoco se llama a ninguno. El único dato que importa
 * de esta conexión es si **existe**: una `HubConnection` viva es
 * evidencia de conectividad más fuerte que cualquier petición HTTP suelta
 * (un WebSocket roto se nota de inmediato; un `IOException` de un solo
 * `GET` puede ser una casualidad puntual). No se suscribe a ningún
 * evento del catálogo de E12.2 — esta UT no consume tiempo real, solo
 * observa si el canal está vivo.
 *
 * El cliente Java de SignalR 8.0.7 **no trae reconexión automática**
 * (`withAutomaticReconnect()` no existe en esta versión — verificado
 * contra el `.jar` real con `javap`, no asumido de la documentación del
 * cliente .NET). Por eso [conectar] es idempotente y segura de llamar en
 * bucle: [ConectividadTicker] la reintenta a cada sondeo mientras no
 * esté `CONNECTED`, con el mismo criterio de "sondeo simple, sin backoff
 * propio" que `EventoSalienteDispatcher`/`NotificacionDispatcher` ya
 * usan del lado del servidor.
 */
@Singleton
class PlantaHubConectividad @Inject constructor(
    private val sesionLocal: SesionLocal,
    private val conectividad: ConectividadRepositorio
) : ConexionTiempoReal {

    private val mutex = Mutex()
    private var conexion: HubConnection? = null

    override fun estaConectado(): Boolean = conexion?.connectionState == HubConnectionState.CONNECTED

    override suspend fun conectar(): Unit = mutex.withLock {
        if (estaConectado()) return@withLock

        val servidorUrl = sesionLocal.servidorUrl() ?: return@withLock
        val token = sesionLocal.tokens()?.accessToken ?: return@withLock
        val urlHub = servidorUrl.trimEnd('/') + "/hub/planta"

        val nuevaConexion = HubConnectionBuilder.create(urlHub)
            .withAccessTokenProvider(Single.just(token))
            .build()

        nuevaConexion.onClosed { conectividad.reportarInalcanzable() }

        withContext(Dispatchers.IO) {
            try {
                nuevaConexion.start().blockingAwait()
                conectividad.reportarAlcanzado()
                conexion = nuevaConexion
            } catch (_: Exception) {
                conectividad.reportarInalcanzable()
            }
        }
    }

    override suspend fun desconectar(): Unit = mutex.withLock {
        val actual = conexion
        conexion = null
        if (actual != null) {
            withContext(Dispatchers.IO) {
                runCatching { actual.stop().blockingAwait() }
            }
        }
    }
}
