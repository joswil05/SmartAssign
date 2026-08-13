package com.smartassign.app.data.cache

import androidx.room.Room
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.smartassign.app.data.personal.PersonaConfirmacion
import kotlinx.coroutines.runBlocking
import net.zetetic.database.sqlcipher.SupportOpenHelperFactory
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/**
 * UT-E13.2 (docs/PROGRESO.md): la caché acotada a su línea (00 §D3) —
 * *"solo el personal de su línea más los físicamente presentes en ella…
 * **nunca el padrón completo**"*, más la regla propia del Coordinador y
 * los disparadores de purga.
 *
 * Instrumentada, contra la base **cifrada de verdad** de E13.1: probar
 * esto contra una base en memoria sin SQLCipher demostraría la lógica
 * pero no que funcione sobre el almacén real.
 */
@RunWith(AndroidJUnit4::class)
class CacheAcotadaALineaTest {

    private val contexto = InstrumentationRegistry.getInstrumentation().targetContext
    private val clave = ClaveCacheKeystore()
    private lateinit var base: CacheDatabase
    private lateinit var repo: CachePersonalRepositorio

    @Before
    fun preparar() {
        contexto.deleteDatabase(NOMBRE_FICHERO_PRUEBA)
        SqlCipherNativo.cargar()
        base = Room.databaseBuilder(contexto, CacheDatabase::class.java, NOMBRE_FICHERO_PRUEBA)
            .openHelperFactory(SupportOpenHelperFactory(clave.contrasenaDeBase()))
            .fallbackToDestructiveMigration()
            .build()
        repo = CachePersonalRepositorio(base.cacheDao())
    }

    @After
    fun limpiar() {
        base.close()
        contexto.deleteDatabase(NOMBRE_FICHERO_PRUEBA)
    }

    private fun persona(id: Int = 1, ficha: String = "F-0001", restricciones: List<String> = listOf("No levantar peso")) =
        PersonaConfirmacion(
            personalId = id, nombreCompleto = "María López", ficha = ficha,
            categoria = "operario", restriccionesMedicas = restricciones
        )

    // ═══ Alcance: "nunca el padrón completo" ═══

    @Test
    fun sin_alcance_abierto_no_se_puede_cachear_a_nadie() {
        // La garantía estructural de la UT: no existe un camino para
        // meter gente en la caché sin declarar antes a qué línea y rol
        // sirve. Sin esto, "nunca el padrón" sería una convención.
        val resultado = runBlocking { repo.guardar(persona()) }

        assertEquals(ResultadoCacheo.SinAlcance, resultado)
        assertEquals(0, runBlocking { base.cacheDao().cuantasPersonas() })
    }

    @Test
    fun con_alcance_de_supervisor_se_cachea_la_persona_con_sus_restricciones() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)

        val resultado = repo.guardar(persona())

        assertEquals(ResultadoCacheo.Guardada(restriccionesGuardadas = true), resultado)
        val recuperada = repo.porFicha("F-0001")
        assertNotNull(recuperada)
        // §12.2: las restricciones activas son requisito PREVIO a
        // consolidar — sin red tienen que seguir estando.
        assertEquals(listOf("No levantar peso"), recuperada!!.restriccionesMedicas)
        assertTrue(repo.alcanceCacheaMedicos())
    }

    @Test
    fun reasignar_la_linea_purga_la_cache_de_la_linea_anterior() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)
        repo.guardar(persona(id = 1, ficha = "F-L4"))
        assertEquals(1, base.cacheDao().cuantasPersonas())

        val hubopurga = repo.abrirAlcance(rol = "supervisor", lineaId = 6)

        assertTrue("cambiar de línea debe purgar (00 §D3)", hubopurga)
        // Si sobreviviera, el supervisor de L6 tendría cacheada gente de
        // L4 — exactamente "más que su línea".
        assertEquals(0, base.cacheDao().cuantasPersonas())
        assertNull(repo.porFicha("F-L4"))
    }

    @Test
    fun reabrir_el_mismo_alcance_no_purga_nada() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)
        repo.guardar(persona())

        val hubopurga = repo.abrirAlcance(rol = "supervisor", lineaId = 4)

        assertFalse("reabrir el mismo alcance no debe tirar la caché", hubopurga)
        assertEquals(1, base.cacheDao().cuantasPersonas())
    }

    @Test
    fun cambiar_de_usuario_en_el_mismo_telefono_purga_aunque_la_linea_no_cambie() = runBlocking {
        // D6: "el teléfono se trata como compartido por línea". Un
        // Coordinador que entra donde estaba un supervisor no puede
        // heredar lo que aquel tenía cacheado.
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)
        repo.guardar(persona())

        val hubopurga = repo.abrirAlcance(rol = "coordinador", lineaId = 4)

        assertTrue(hubopurga)
        assertEquals(0, base.cacheDao().cuantasPersonas())
    }

    // ═══ Coordinador: nunca cachea restricciones médicas ═══

    @Test
    fun el_coordinador_cachea_la_persona_pero_nunca_sus_restricciones_medicas() = runBlocking {
        // 00 §D3, literal: "su dispositivo no cachea restricciones
        // médicas de las 10 líneas: las consulta en línea bajo demanda…
        // precargarlo convertiría un teléfono extraviado en una fuga del
        // padrón médico completo".
        repo.abrirAlcance(rol = "coordinador", lineaId = null)

        val resultado = repo.guardar(persona(restricciones = listOf("No levantar peso", "No exposición a frío")))

        assertEquals(ResultadoCacheo.Guardada(restriccionesGuardadas = false), resultado)
        // La identidad sí (la necesita para confirmar, §12.2)…
        assertEquals("María López", repo.porFicha("F-0001")!!.nombreCompleto)
        // …pero ni una sola restricción llega al disco.
        assertEquals(0, base.cacheDao().cuantasRestricciones())
    }

    @Test
    fun para_un_coordinador_la_lista_vacia_no_significa_sin_restricciones() = runBlocking {
        repo.abrirAlcance(rol = "coordinador", lineaId = null)
        repo.guardar(persona(restricciones = listOf("No levantar peso")))

        // §12.4: presentar esa lista vacía como "no tiene restricciones"
        // sería la mentira exacta que §12.2 quiere impedir. El repositorio
        // expone la diferencia para que la interfaz no pueda confundirlas.
        assertFalse(repo.alcanceCacheaMedicos())
        assertTrue(repo.porFicha("F-0001")!!.restriccionesMedicas.isEmpty())
    }

    // ═══ Purga ═══

    @Test
    fun purgar_borra_personas_restricciones_y_el_propio_alcance() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)
        repo.guardar(persona(id = 1, ficha = "F-0001"))
        repo.guardar(persona(id = 2, ficha = "F-0002", restricciones = listOf("Turno diurno")))

        repo.purgar()

        assertEquals(0, base.cacheDao().cuantasPersonas())
        assertEquals(0, base.cacheDao().cuantasRestricciones())
        // Y el alcance también: una caché vacía pero todavía "abierta"
        // aceptaría escrituras nuevas sin que nadie volviera a declarar
        // para quién.
        assertNull(repo.alcanceVigente())
        assertEquals(ResultadoCacheo.SinAlcance, repo.guardar(persona()))
    }

    @Test
    fun el_alcance_vigente_queda_registrado_con_su_rol_y_su_linea() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 8)

        val alcance = repo.alcanceVigente()

        assertNotNull(alcance)
        assertEquals("supervisor", alcance!!.rol)
        assertEquals(8, alcance.lineaId)
    }

    @Test
    fun el_alcance_sobrevive_a_cerrar_y_reabrir_la_base() = runBlocking {
        repo.abrirAlcance(rol = "supervisor", lineaId = 4)
        repo.guardar(persona())
        base.close()

        SqlCipherNativo.cargar()
        base = Room.databaseBuilder(contexto, CacheDatabase::class.java, NOMBRE_FICHERO_PRUEBA)
            .openHelperFactory(SupportOpenHelperFactory(clave.contrasenaDeBase()))
            .fallbackToDestructiveMigration()
            .build()
        repo = CachePersonalRepositorio(base.cacheDao())

        // §12.1: sin red la app debe comportarse igual que conectada —
        // si el alcance se perdiera al reiniciar, la caché quedaría
        // inutilizable justo cuando más falta hace.
        assertEquals(4, repo.alcanceVigente()?.lineaId)
        assertNotNull(repo.porFicha("F-0001"))
    }

    private companion object {
        const val NOMBRE_FICHERO_PRUEBA = "smartassign_cache_alcance_prueba.db"
    }
}
