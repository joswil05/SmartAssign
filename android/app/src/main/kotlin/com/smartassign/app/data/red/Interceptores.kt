package com.smartassign.app.data.red

import com.smartassign.app.data.conectividad.ConectividadRepositorio
import com.smartassign.app.data.sesion.SesionLocal
import okhttp3.Interceptor
import okhttp3.Response
import okhttp3.HttpUrl.Companion.toHttpUrlOrNull
import java.io.IOException
import javax.inject.Inject

/**
 * La URL del servidor de planta no se conoce hasta que el Coordinador la
 * entrega por QR (F3, 02 §1.0) — Retrofit exige una `baseUrl` fija al
 * construirse, así que se le da un marcador de posición y este
 * interceptor reescribe esquema/host/puerto con la URL real guardada,
 * en cada petición. Si todavía no hay URL guardada, deja pasar la
 * petición tal cual (solo ocurre durante la propia verificación del QR,
 * que no usa este cliente — ver `SesionRepositorioImpl.verificarServidor`).
 */
class InterceptorUrlServidor @Inject constructor(private val sesionLocal: SesionLocal) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val original = chain.request()
        val base = sesionLocal.servidorUrl()?.toHttpUrlOrNull() ?: return chain.proceed(original)

        val reescrita = original.url.newBuilder()
            .scheme(base.scheme)
            .host(base.host)
            .port(base.port)
            .build()

        return chain.proceed(original.newBuilder().url(reescrita).build())
    }
}

/**
 * Adjunta el access token a toda petición salvo las que la Api marca
 * `AllowAnonymous` (`AuthEndpoints.cs`, `ServidorEndpoints.cs`) — el mismo
 * agrupamiento que el servidor ya declara, replicado aquí porque el
 * cliente no puede preguntarle al servidor "¿esto necesita token?".
 */
class InterceptorAutorizacion @Inject constructor(private val sesionLocal: SesionLocal) : Interceptor {
    private val rutasAnonimas = setOf(
        "api/auth/login", "api/auth/refresh", "api/auth/pin", "api/servidor/info",
        "api/version-app/actual", "api/version-app/apk", // E14.6 — se comprueba antes de que exista sesión
    )

    override fun intercept(chain: Interceptor.Chain): Response {
        val peticion = chain.request()
        val ruta = peticion.url.encodedPath.removePrefix("/")
        if (ruta in rutasAnonimas) return chain.proceed(peticion)

        val token = sesionLocal.tokens()?.accessToken ?: return chain.proceed(peticion)
        return chain.proceed(peticion.newBuilder().header("Authorization", "Bearer $token").build())
    }
}

/**
 * UT-E13.3 (00 §12.1, 05 §4.3): alimenta [ConectividadRepositorio] con
 * la única señal real de "¿se llegó al servidor de planta?" — cualquier
 * respuesta HTTP, sea 2xx o un rechazo, prueba que se le alcanzó; solo
 * una `IOException` (sin respuesta en absoluto) es "sin conexión". Un
 * choke point único, como `InterceptorUrlServidor`/`InterceptorAutorizacion`:
 * toda petición del cliente autenticado pasa por aquí, así que ningún
 * repositorio nuevo puede olvidarse de reportar su resultado.
 */
class InterceptorConectividad @Inject constructor(
    private val conectividad: ConectividadRepositorio
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val respuesta = try {
            chain.proceed(chain.request())
        } catch (e: IOException) {
            conectividad.reportarInalcanzable()
            throw e
        }
        conectividad.reportarAlcanzado()
        return respuesta
    }
}
