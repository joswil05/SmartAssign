package com.smartassign.app.data.conectividad

/**
 * UT-E13.5 (05 §4.3): la mitad de la detección que no es el latido —
 * "el estado de la conexión SignalR". `PlantaHubConectividad` es la
 * única implementación real; esta interfaz existe para que
 * `SesionRepositorioImpl` (el ciclo de sesión, E2.2/E13.2) no tenga que
 * conocer `HubConnection` ni RxJava — mismo criterio exacto que
 * `PurgaCacheLocal` (E13.2) para la caché cifrada.
 */
interface ConexionTiempoReal {
    /** No-op si ya hay una conexión activa o si todavía no hay servidor/sesión. */
    suspend fun conectar()

    suspend fun desconectar()

    /** Para que [ConectividadTicker] sepa si vale la pena reintentar en este sondeo. */
    fun estaConectado(): Boolean
}
