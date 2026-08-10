package com.smartassign.app.data.malla

import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.InterceptorAutorizacion
import com.smartassign.app.data.red.InterceptorUrlServidor
import com.smartassign.app.data.red.LoginRequest
import com.smartassign.app.data.red.MallaApi
import com.smartassign.app.data.sesion.FakeSesionLocal
import com.smartassign.app.data.sesion.SesionLocal
import com.smartassign.app.data.sesion.TokensGuardados
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import java.time.Instant
import java.util.concurrent.TimeUnit

/**
 * Contra la Api real de esta rama (`SmartAssignAndroidJvmTest2`, puerto
 * 5081, ver docs/PROGRESO.md E6.4) — un puesto real sembrado a mano con
 * `sqlcmd` (`L4-JVM01`, línea 9, fijo, sin ninguna jornada abierta, así
 * que su situación real es `fuera_de_operacion`). La variedad de
 * situaciones/micro-copia ya está probada exhaustivamente en el backend
 * (`MallaLineaEndpointTests.cs`, 9 pruebas) — aquí solo se valida que el
 * contrato HTTP/JSON real deserializa correctamente en el cliente.
 */
class MallaRepositorioIntegrationTest {

    private val urlServidor = "http://localhost:5081/"

    private fun nuevoRepositorio(local: SesionLocal): MallaRepositorio {
        val json = Json { ignoreUnknownKeys = true }
        val cliente = OkHttpClient.Builder()
            .addInterceptor(InterceptorUrlServidor(local))
            .addInterceptor(InterceptorAutorizacion(local))
            .connectTimeout(5, TimeUnit.SECONDS)
            .readTimeout(5, TimeUnit.SECONDS)
            .build()
        val retrofit = Retrofit.Builder()
            .baseUrl(urlServidor)
            .client(cliente)
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
        return MallaRepositorioImpl(retrofit.create(MallaApi::class.java))
    }

    private suspend fun iniciarSesionRealAsync(local: SesionLocal, username: String, password: String) {
        local.guardarServidorUrl(urlServidor)
        val json = Json { ignoreUnknownKeys = true }
        val retrofit = Retrofit.Builder()
            .baseUrl(urlServidor)
            .client(OkHttpClient.Builder().addInterceptor(InterceptorUrlServidor(local)).build())
            .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
            .build()
        val authApi = retrofit.create(AuthApi::class.java)
        val respuesta = authApi.login(LoginRequest(username, password, local.deviceId()))
        check(respuesta.isSuccessful) { "Login real falló: ${respuesta.code()} ${respuesta.errorBody()?.string()}" }
        val cuerpo = respuesta.body()!!
        local.guardarTokens(
            TokensGuardados(cuerpo.accessToken, Instant.parse(cuerpo.accessExpiraEn), cuerpo.refreshToken, cuerpo.refreshExpiraEn?.let(Instant::parse))
        )
    }

    @Test
    fun un_supervisor_ve_el_puesto_real_sembrado_en_su_linea() = runTest {
        val local = FakeSesionLocal()
        iniciarSesionRealAsync(local, "sup_l4_malla", "Clave#Malla123")
        val repo = nuevoRepositorio(local)

        val resultado = repo.puestosDeLinea(9)

        assertTrue(resultado is ResultadoMalla.Ok)
        val puestos = (resultado as ResultadoMalla.Ok).puestos
        val puesto = puestos.single { it.codigo == "L4-JVM01" }
        assertEquals("fijo", puesto.tipo)
        // Sin ninguna jornada abierta para L4 en esta base descartable —
        // fuera de operación es la situación real, no simulada.
        assertEquals("fuera_de_operacion", puesto.situacion)
        assertEquals("Puesto no requerido por el SKU de hoy", puesto.microCopia)
    }

    @Test
    fun un_supervisor_de_otra_linea_recibe_sin_alcance_de_verdad() = runTest {
        val local = FakeSesionLocal()
        iniciarSesionRealAsync(local, "sup_l1_malla", "Clave#Malla456")
        val repo = nuevoRepositorio(local)

        val resultado = repo.puestosDeLinea(9) // sup_l1_malla solo tiene L1, no L9

        assertTrue(resultado is ResultadoMalla.SinAlcance)
    }
}
