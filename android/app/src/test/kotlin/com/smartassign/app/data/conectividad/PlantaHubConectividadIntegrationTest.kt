package com.smartassign.app.data.conectividad

import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.LoginRequest
import com.smartassign.app.data.sesion.FakeSesionLocal
import com.smartassign.app.data.sesion.TokensGuardados
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import java.time.Instant
import java.util.concurrent.TimeUnit

/**
 * UT-E13.5 (05 §4.3) — `PlantaHubConectividad` contra el `PlantaHub`
 * REAL de esta rama (`SmartAssignAndroidJvmTest2`, puerto 5081, mismo
 * fixture que `SesionRepositorioIntegrationTest`), no un doble.
 *
 * Corre como prueba de JVM (`testDebugUnitTest`), sin emulador: el
 * cliente de SignalR (`com.microsoft.signalr:signalr`) es una librería
 * Java pura sobre OkHttp — no depende del runtime de Android para
 * conectarse, igual que Retrofit ya no lo necesitaba en
 * `SesionRepositorioIntegrationTest`. Es la verificación de punta a
 * punta real que le falta a `ConectividadTickerTest` (que solo prueba el
 * sondeo con dobles).
 */
class PlantaHubConectividadIntegrationTest {

    private val urlServidor = "http://localhost:5081/"

    private suspend fun tokenRealDeCoordinador(): String {
        val json = Json { ignoreUnknownKeys = true }
        val cliente = OkHttpClient.Builder()
            .connectTimeout(5, TimeUnit.SECONDS)
            .readTimeout(5, TimeUnit.SECONDS)
            .build()
        val retrofit = Retrofit.Builder()
            .baseUrl(urlServidor)
            .client(cliente)
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
        val api = retrofit.create(AuthApi::class.java)
        val respuesta = api.login(LoginRequest("coord_android", "Clave#Coord123", "device-hub-jvm-test"))
        check(respuesta.isSuccessful) { "Login real falló: ${respuesta.code()} ${respuesta.errorBody()?.string()}" }
        return respuesta.body()!!.accessToken
    }

    private suspend fun localConTokenReal(): FakeSesionLocal {
        val local = FakeSesionLocal()
        local.guardarServidorUrl(urlServidor)
        local.guardarTokens(
            TokensGuardados(
                accessToken = tokenRealDeCoordinador(),
                accessExpiraEn = Instant.now().plusSeconds(900),
                refreshToken = null,
                refreshExpiraEn = null
            )
        )
        return local
    }

    @Test
    fun conectar_contra_el_hub_real_marca_la_conectividad_como_alcanzada() = runTest {
        val local = localConTokenReal()
        val conectividad = ConectividadRepositorioImpl()
        conectividad.reportarInalcanzable() // arranca desde "sin conexión" a propósito
        val hub = PlantaHubConectividad(local, conectividad)

        hub.conectar()

        assertTrue("un handshake real con /hub/planta debió completarse", hub.estaConectado())
        assertEquals(EstadoConectividad.Conectado, conectividad.estado.value)

        hub.desconectar()
    }

    @Test
    fun conectar_dos_veces_seguidas_no_abre_una_segunda_conexion() = runTest {
        val local = localConTokenReal()
        val hub = PlantaHubConectividad(local, ConectividadRepositorioImpl())

        hub.conectar()
        assertTrue(hub.estaConectado())
        hub.conectar() // no debe lanzar ni reemplazar la conexión ya viva

        assertTrue(hub.estaConectado())

        hub.desconectar()
    }

    @Test
    fun desconectar_deja_estaConectado_en_falso() = runTest {
        val local = localConTokenReal()
        val hub = PlantaHubConectividad(local, ConectividadRepositorioImpl())
        hub.conectar()
        assertTrue(hub.estaConectado())

        hub.desconectar()

        assertFalse(hub.estaConectado())
    }

    @Test
    fun sin_token_conectar_no_intenta_nada_y_no_lanza() = runTest {
        val local = FakeSesionLocal().apply { guardarServidorUrl(urlServidor) } // sin tokens
        val conectividad = ConectividadRepositorioImpl()
        val hub = PlantaHubConectividad(local, conectividad)

        hub.conectar() // no debe lanzar

        assertFalse(hub.estaConectado())
        // Y no debe fingir un resultado: ni conectado ni "reportado inalcanzable"
        // por un intento que ni siquiera se hizo — sigue en el optimista de arranque.
        assertEquals(EstadoConectividad.Conectado, conectividad.estado.value)
    }

    @Test
    fun un_token_invalido_reporta_inalcanzable_sin_lanzar() = runTest {
        val local = FakeSesionLocal().apply {
            guardarServidorUrl(urlServidor)
            guardarTokens(TokensGuardados(accessToken = "esto-no-es-un-jwt-valido", accessExpiraEn = Instant.now().plusSeconds(900), refreshToken = null, refreshExpiraEn = null))
        }
        val conectividad = ConectividadRepositorioImpl()
        val hub = PlantaHubConectividad(local, conectividad)

        hub.conectar()

        assertFalse(hub.estaConectado())
        assertEquals(EstadoConectividad.SinConexion, conectividad.estado.value)
    }
}
