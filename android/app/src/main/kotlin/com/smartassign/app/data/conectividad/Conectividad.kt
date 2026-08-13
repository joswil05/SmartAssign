package com.smartassign.app.data.conectividad

import kotlinx.coroutines.flow.StateFlow

/**
 * UT-E13.3 (docs/PROGRESO.md): "Bloqueo defensivo · no se encola nada"
 * (§12.1, 05 §4.3).
 *
 * Binario a propósito — 05 §4.3 reduce todo el comportamiento sin
 * conexión a una sola pregunta ("¿LECTURA desde caché, ESCRITURA
 * bloqueada?"), nunca a un nivel de señal o una cuenta de reintentos.
 */
sealed interface EstadoConectividad {
    data object Conectado : EstadoConectividad
    data object SinConexion : EstadoConectividad
}

/**
 * La fuente de verdad de "¿podemos escribir ahora mismo?" — un único
 * estado observable, no una comprobación que cada pantalla repita a su
 * manera.
 *
 * **Cómo se alimenta hoy, y por qué es deliberadamente parcial.** Cada
 * respuesta HTTP real —éxito o error del SERVIDOR, un 4xx/5xx cuenta como
 * "alcanzado"— actualiza este estado vía `InterceptorConectividad`
 * (`data/red/Interceptores.kt`), el mismo patrón ya establecido para
 * `InterceptorUrlServidor`/`InterceptorAutorizacion`. Es un signal más
 * fuerte que `ConnectivityManager.NetworkCapabilities` —05 §4.3, literal,
 * "no basta": Wi-Fi asociado sin llegar al servidor no debe contar como
 * conectado— porque mide alcanzabilidad real del servidor de planta, no
 * asociación de radio. Pero es **reactivo**: solo se entera en el
 * siguiente intento real de red, no lo detecta mientras el supervisor
 * simplemente mira una pantalla cargada. Cerrar ese hueco con un latido
 * periódico + el estado de la conexión SignalR es **E13.5** ("Detección
 * por latido") — mismo LEE (`05 §4.3`) que esta UT, UT distinta a
 * propósito. Este repositorio ya queda listo para que ese latido futuro
 * llame a los mismos dos métodos.
 */
interface ConectividadRepositorio {
    val estado: StateFlow<EstadoConectividad>

    /** Cualquier respuesta HTTP real del servidor de planta, sea 2xx o un rechazo — la prueba de que se le alcanzó. */
    fun reportarAlcanzado()

    /** Un intento real de red terminó en `IOException` — sin respuesta del servidor en absoluto. */
    fun reportarInalcanzable()
}
