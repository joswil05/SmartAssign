package com.smartassign.app.data.sesion

import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.red.InterceptorAutorizacion
import com.smartassign.app.data.red.InterceptorUrlServidor
import kotlinx.coroutines.test.runTest
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import retrofit2.Retrofit
import java.util.concurrent.TimeUnit

/**
 * Contra la Api real de esta misma rama, levantada de verdad
 * (`dotnet run`, `SmartAssignAndroidJvmTest`, ver docs/PROGRESO.md E6.3) —
 * no un servidor simulado. Corre como prueba de JVM (`testDebugUnitTest`):
 * la petición sale del proceso de pruebas directo a `localhost`, sin
 * pasar por el runtime de Android, así que no hace falta emulador ni
 * configuración de red de Android para este nivel.
 *
 * Usuarios de prueba creados con
 * `ImportadorCli crear-usuario-prueba` (E6.3, contraseñas reales
 * verificables, no simuladas):
 *   - coord_android          / Clave#Coord123      (coordinador)
 *   - sup_l4_android         / Clave#SupL4123      (supervisor de L4)
 *   - sup_l8_android         / Clave#SupL8123      (supervisor de L8, Bolsón)
 *   - sup_sinlinea_android   / Clave#SinLinea123   (supervisor sin línea)
 *
 * Si esta base no está levantada, las pruebas fallan con SinConexion —
 * fallan de forma clara, no silenciosamente en verde.
 */
class SesionRepositorioIntegrationTest {

    private val urlServidor = "http://localhost:5080/"

    private fun nuevoRepositorio(local: SesionLocal = FakeSesionLocal()): SesionRepositorio {
        local.guardarServidorUrl(urlServidor)
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
        val api = retrofit.create(AuthApi::class.java)
        val clienteCrudo = OkHttpClient.Builder()
            .connectTimeout(5, TimeUnit.SECONDS)
            .readTimeout(5, TimeUnit.SECONDS)
            .build()
        return SesionRepositorioImpl(api, local, json, clienteCrudo)
    }

    @Test
    fun verificarServidor_contra_la_api_real_responde_true() = runTest {
        val repo = nuevoRepositorio()
        assertTrue(repo.verificarServidor(urlServidor))
    }

    @Test
    fun verificarServidor_contra_un_puerto_sin_nada_escuchando_responde_false() = runTest {
        val repo = nuevoRepositorio()
        assertFalse(repo.verificarServidor("http://localhost:1/"))
    }

    @Test
    fun login_con_credenciales_reales_de_coordinador_es_exitoso() = runTest {
        val repo = nuevoRepositorio()
        val resultado = repo.iniciarSesion("coord_android", "Clave#Coord123")

        assertTrue(resultado is ResultadoAuth.Ok)
        assertEquals("coordinador", (resultado as ResultadoAuth.Ok).rol)
    }

    @Test
    fun login_con_contrasena_equivocada_rechaza_con_credenciales_invalidas() = runTest {
        val repo = nuevoRepositorio()
        val resultado = repo.iniciarSesion("coord_android", "esto-no-es-la-clave")

        assertTrue(resultado is ResultadoAuth.Rechazo)
        assertEquals("CREDENCIALES_INVALIDAS", (resultado as ResultadoAuth.Rechazo).codigo)
    }

    @Test
    fun login_con_usuario_inexistente_rechaza_con_el_mismo_codigo_que_password_equivocada() = runTest {
        // 02 §1.1: un solo error, sin detalle de qué falló — ver también la
        // prueba equivalente de contraseña equivocada: mismo código.
        val repo = nuevoRepositorio()
        val resultado = repo.iniciarSesion("nadie_con_este_username", "cualquier-cosa")

        assertEquals("CREDENCIALES_INVALIDAS", (resultado as ResultadoAuth.Rechazo).codigo)
    }

    @Test
    fun quienSoy_de_un_supervisor_de_L4_trae_su_linea_vigente_no_bolson() = runTest {
        val repo = nuevoRepositorio()
        val login = repo.iniciarSesion("sup_l4_android", "Clave#SupL4123")
        assertTrue(login is ResultadoAuth.Ok)

        val quienSoy = repo.quienSoy()
        assertNotNull(quienSoy)
        assertEquals("supervisor", quienSoy!!.rol)
        assertNotNull(quienSoy.linea)
        assertEquals(4, quienSoy.linea!!.id)
        assertFalse(quienSoy.linea!!.esBolson)
    }

    @Test
    fun quienSoy_de_un_supervisor_de_L8_marca_esBolson() = runTest {
        val repo = nuevoRepositorio()
        repo.iniciarSesion("sup_l8_android", "Clave#SupL8123")

        val quienSoy = repo.quienSoy()
        assertTrue(quienSoy!!.linea!!.esBolson)
    }

    @Test
    fun quienSoy_de_un_supervisor_sin_linea_trae_linea_nula() = runTest {
        // §2.3: la rama <¿Tiene línea asignada?> = NO existe de verdad.
        val repo = nuevoRepositorio()
        repo.iniciarSesion("sup_sinlinea_android", "Clave#SinLinea123")

        val quienSoy = repo.quienSoy()
        assertNull(quienSoy!!.linea)
    }

    @Test
    fun quienSoy_sin_haber_iniciado_sesion_falla_porque_no_hay_token() = runTest {
        val repo = nuevoRepositorio()
        assertNull(repo.quienSoy())
    }

    @Test
    fun pin_sin_configurar_en_el_servidor_se_rechaza_con_el_codigo_real() = runTest {
        // Los usuarios de prueba tienen contraseña real pero ningún PIN
        // configurado — exactamente el camino PIN_NO_CONFIGURADO de
        // ServicioAutenticacion.ReentrarConPinAsync.
        val repo = nuevoRepositorio()
        repo.iniciarSesion("coord_android", "Clave#Coord123")

        val resultado = repo.reentrarConPin("1234")
        assertEquals("PIN_NO_CONFIGURADO", (resultado as ResultadoAuth.Rechazo).codigo)
    }

    @Test
    fun renovarSesion_con_el_refresh_token_real_obtiene_un_access_token_nuevo() = runTest {
        val repo = nuevoRepositorio()
        val primero = repo.iniciarSesion("coord_android", "Clave#Coord123") as ResultadoAuth.Ok

        val renovado = repo.renovarSesion()
        assertTrue(renovado is ResultadoAuth.Ok)
        assertEquals(primero.usuarioId, (renovado as ResultadoAuth.Ok).usuarioId)
    }

    @Test
    fun cerrarSesion_real_purga_lo_local_incluso_si_el_servidor_ya_no_reconoce_el_token() = runTest {
        val local = FakeSesionLocal()
        val repo = nuevoRepositorio(local)
        repo.iniciarSesion("coord_android", "Clave#Coord123")
        assertNotNull(local.tokens())

        repo.cerrarSesion()

        assertNull(local.tokens())
        assertNull(local.identidad())
    }

    @Test
    fun haySesionGuardada_es_falso_antes_de_cualquier_login() {
        val repo = nuevoRepositorio()
        assertFalse(repo.haySesionGuardada())
    }

    @Test
    fun haySesionGuardada_es_verdadero_justo_despues_de_iniciar_sesion() = runTest {
        val repo = nuevoRepositorio()
        repo.iniciarSesion("coord_android", "Clave#Coord123")
        assertTrue(repo.haySesionGuardada())
    }
}
