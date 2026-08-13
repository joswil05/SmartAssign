package com.smartassign.app.data.red

import com.smartassign.app.data.conectividad.ConectividadRepositorioImpl
import com.smartassign.app.data.conectividad.EstadoConectividad
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.mockwebserver.MockResponse
import okhttp3.mockwebserver.MockWebServer
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Before
import org.junit.Test
import java.io.IOException
import java.util.concurrent.TimeUnit

/**
 * UT-E13.3 — contra un servidor HTTP real y efímero (`MockWebServer`),
 * no un doble de `Interceptor.Chain` a mano: lo que hay que probar es
 * que un ciclo de petición/respuesta real actualiza [EstadoConectividad],
 * no que la lógica interna llame a los métodos en el orden esperado.
 */
class InterceptorConectividadTest {

    private lateinit var servidor: MockWebServer
    private lateinit var conectividad: ConectividadRepositorioImpl
    private lateinit var cliente: OkHttpClient

    @Before
    fun preparar() {
        servidor = MockWebServer()
        servidor.start()
        conectividad = ConectividadRepositorioImpl()
        cliente = OkHttpClient.Builder()
            .addInterceptor(InterceptorConectividad(conectividad))
            .connectTimeout(1, TimeUnit.SECONDS)
            .readTimeout(1, TimeUnit.SECONDS)
            .build()
    }

    @After
    fun apagar() {
        // Una de las pruebas apaga el servidor ella misma para forzar la
        // IOException — un segundo shutdown() aquí es un no-op tolerado.
        runCatching { servidor.shutdown() }
    }

    private fun peticion() = Request.Builder().url(servidor.url("/")).build()

    @Test
    fun una_respuesta_2xx_reporta_alcanzado() {
        conectividad.reportarInalcanzable() // arranca desde "sin conexión" a propósito
        servidor.enqueue(MockResponse().setResponseCode(200))

        cliente.newCall(peticion()).execute().close()

        assertEquals(EstadoConectividad.Conectado, conectividad.estado.value)
    }

    @Test
    fun un_rechazo_4xx_del_servidor_tambien_reporta_alcanzado() {
        // El servidor RESPONDIÓ — que haya rechazado la petición es un
        // asunto de negocio, no de conectividad. Confundir los dos
        // bloquearía escrituras nuevas por culpa de un 409 ajeno.
        conectividad.reportarInalcanzable()
        servidor.enqueue(MockResponse().setResponseCode(409))

        cliente.newCall(peticion()).execute().close()

        assertEquals(EstadoConectividad.Conectado, conectividad.estado.value)
    }

    @Test
    fun un_5xx_tambien_cuenta_como_alcanzado() {
        conectividad.reportarInalcanzable()
        servidor.enqueue(MockResponse().setResponseCode(500))

        cliente.newCall(peticion()).execute().close()

        assertEquals(EstadoConectividad.Conectado, conectividad.estado.value)
    }

    @Test
    fun sin_respuesta_del_servidor_reporta_inalcanzable_y_relanza_la_excepcion() {
        servidor.shutdown() // nadie escucha en ese puerto — IOException real, no simulada

        var seLanzo = false
        try {
            cliente.newCall(peticion()).execute()
        } catch (_: IOException) {
            seLanzo = true
        }

        assertEquals(true, seLanzo)
        assertEquals(EstadoConectividad.SinConexion, conectividad.estado.value)
    }
}
