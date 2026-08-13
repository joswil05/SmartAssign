package com.smartassign.app.data.conectividad

import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.sesion.SesionLocal
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

/**
 * UT-E13.5 (05 §4.3): el "latido" en sí — el sondeo periódico que
 * `ConectividadRepositorio` (E13.3) necesitaba y no tenía. E13.3 ya dejó
 * anotado el hueco exacto: su señal es reactiva, "solo se entera en el
 * siguiente intento real de red, no lo detecta mientras el supervisor
 * simplemente mira una pantalla ya cargada". Este ticker es lo que cierra
 * eso — corre SIEMPRE, haya o no una pantalla pidiendo datos.
 *
 * Cada sondeo hace las DOS cosas que 05 §4.3 pide explícitamente:
 * 1. Un `GET /api/servidor/info` (liviano, anónimo) por el cliente
 *    autenticado — pasa por `InterceptorConectividad` (E13.3) igual que
 *    cualquier otra petición real, sin código nuevo para reportar el
 *    resultado.
 * 2. Si `PlantaHubConectividad` no está `CONNECTED`, reintenta — el
 *    "estado de la conexión SignalR" de la fuente.
 *
 * Nada de esto ocurre si todavía no hay servidor configurado (alta por
 * QR sin terminar) — sondear contra la URL marcador de posición
 * produciría un `SinConexion` falso antes de que exista nada que probar.
 *
 * Vive para toda la vida del proceso — se arranca una sola vez desde
 * `SmartAssignApplication.onCreate()`, mismo criterio que un
 * `BackgroundService` del lado del servidor (`EventoSalienteDispatcher`,
 * E12.3): dueño de su propio `CoroutineScope`, porque no hay ninguna
 * `Activity`/`ViewModel` cuyo ciclo de vida sea "toda la app".
 */
@Singleton
class ConectividadTicker @Inject constructor(
    private val authApi: AuthApi,
    private val sesionLocal: SesionLocal,
    private val plantaHub: ConexionTiempoReal
) {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    @Volatile
    private var iniciado = false

    fun iniciar() {
        if (iniciado) return
        iniciado = true
        scope.launch {
            while (isActive) {
                try {
                    sondear()
                } catch (_: Exception) {
                    // Un sondeo fallido no debe matar el bucle — el
                    // siguiente sondeo, 15 s después, lo intenta de nuevo.
                }
                delay(INTERVALO_DE_SONDEO_MS)
            }
        }
    }

    internal suspend fun sondear() {
        if (sesionLocal.servidorUrl() == null) return // alta por QR sin terminar (E6.3) — nada que sondear todavía

        runCatching { authApi.servidorInfo() } // el resultado real lo procesa InterceptorConectividad, no esta función

        if (!plantaHub.estaConectado()) {
            plantaHub.conectar()
        }
    }

    private companion object {
        const val INTERVALO_DE_SONDEO_MS = 15_000L
    }
}
