package com.smartassign.app.data.conectividad

import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.LoginRequest
import com.smartassign.app.data.red.LogoutRequest
import com.smartassign.app.data.red.MeResponse
import com.smartassign.app.data.red.PinRequest
import com.smartassign.app.data.red.RefreshRequest
import com.smartassign.app.data.red.ServidorInfoResponse
import com.smartassign.app.data.sesion.FakeSesionLocal
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test
import retrofit2.Response

/**
 * UT-E13.5 (05 §4.3) — el sondeo en sí, sin red real ni `HubConnection`
 * de por medio: un `AuthApi` y un `ConexionTiempoReal` falsos que solo
 * cuentan invocaciones.
 */
class ConectividadTickerTest {

    private class ApiDeConteo : AuthApi {
        var vecesServidorInfo = 0
        override suspend fun login(cuerpo: LoginRequest): Response<com.smartassign.app.data.red.SesionResponse> = throw NotImplementedError()
        override suspend fun refresh(cuerpo: RefreshRequest): Response<com.smartassign.app.data.red.SesionResponse> = throw NotImplementedError()
        override suspend fun pin(cuerpo: PinRequest): Response<com.smartassign.app.data.red.SesionResponse> = throw NotImplementedError()
        override suspend fun logout(cuerpo: LogoutRequest): Response<Unit> = throw NotImplementedError()
        override suspend fun me(): Response<MeResponse> = throw NotImplementedError()
        override suspend fun servidorInfo(): Response<ServidorInfoResponse> {
            vecesServidorInfo++
            return Response.success(ServidorInfoResponse(servidor = "SmartAssign"))
        }
    }

    private class ConexionDeConteo(private var conectado: Boolean = false) : ConexionTiempoReal {
        var vecesConectar = 0
        override suspend fun conectar() { vecesConectar++; conectado = true }
        override suspend fun desconectar() { conectado = false }
        override fun estaConectado() = conectado
    }

    @Test
    fun sin_url_de_servidor_configurada_no_sondea_nada() = runTest {
        val api = ApiDeConteo()
        val conexion = ConexionDeConteo()
        val local = FakeSesionLocal() // sin guardarServidorUrl — alta por QR sin terminar
        val ticker = ConectividadTicker(api, local, conexion)

        ticker.sondear()

        assertEquals(0, api.vecesServidorInfo)
        assertEquals(0, conexion.vecesConectar)
    }

    @Test
    fun con_url_configurada_llama_al_latido_http() = runTest {
        val api = ApiDeConteo()
        val local = FakeSesionLocal().apply { guardarServidorUrl("http://localhost:5081/") }
        val ticker = ConectividadTicker(api, local, ConexionDeConteo())

        ticker.sondear()

        assertEquals(1, api.vecesServidorInfo)
    }

    @Test
    fun si_el_hub_no_esta_conectado_reintenta_conectar() = runTest {
        val local = FakeSesionLocal().apply { guardarServidorUrl("http://localhost:5081/") }
        val conexion = ConexionDeConteo(conectado = false)
        val ticker = ConectividadTicker(ApiDeConteo(), local, conexion)

        ticker.sondear()

        assertEquals(1, conexion.vecesConectar)
    }

    @Test
    fun si_el_hub_ya_esta_conectado_no_reintenta() = runTest {
        val local = FakeSesionLocal().apply { guardarServidorUrl("http://localhost:5081/") }
        val conexion = ConexionDeConteo(conectado = true)
        val ticker = ConectividadTicker(ApiDeConteo(), local, conexion)

        ticker.sondear()

        assertEquals(0, conexion.vecesConectar)
    }
}
