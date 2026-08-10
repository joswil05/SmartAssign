package com.smartassign.app.data.personal

import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.smartassign.app.data.red.InterceptorAutorizacion
import com.smartassign.app.data.red.InterceptorUrlServidor
import com.smartassign.app.data.red.PersonalApi
import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.LoginRequest
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
 * 5081, ver docs/PROGRESO.md E6.4/E6.6) — una persona real sembrada a
 * mano con `sqlcmd` (ficha `F-JVM01`, categoría `operario`, con una
 * restricción médica permanente y vigente sobre `CAP-JVM01`).
 */
class PersonalRepositorioIntegrationTest {

    private val urlServidor = "http://localhost:5081/"

    private fun nuevoRepositorio(local: SesionLocal): PersonalRepositorio {
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
        return PersonalRepositorioImpl(retrofit.create(PersonalApi::class.java))
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
    fun una_ficha_real_trae_nombre_categoria_y_restricciones_vigentes_explicitas() = runTest {
        val local = FakeSesionLocal()
        iniciarSesionRealAsync(local, "sup_l4_android", "Clave#SupL4123")
        val repo = nuevoRepositorio(local)

        val resultado = repo.porFicha("F-JVM01")

        assertTrue(resultado is ResultadoPersonal.Ok)
        val persona = (resultado as ResultadoPersonal.Ok).persona
        assertEquals("María López Hernández", persona.nombreCompleto)
        assertEquals("operario", persona.categoria)
        assertEquals(listOf("No levantar carga superior a 10 kg"), persona.restriccionesMedicas)
    }

    @Test
    fun una_ficha_que_no_existe_en_el_padron_real_trae_no_encontrado() = runTest {
        val local = FakeSesionLocal()
        iniciarSesionRealAsync(local, "sup_l4_android", "Clave#SupL4123")
        val repo = nuevoRepositorio(local)

        val resultado = repo.porFicha("esta-ficha-no-existe-en-la-base")

        assertTrue(resultado is ResultadoPersonal.NoEncontrado)
    }
}
