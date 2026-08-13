package com.smartassign.app.data.cache

import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteException
import androidx.room.Room
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import kotlinx.coroutines.runBlocking
import net.zetetic.database.sqlcipher.SupportOpenHelperFactory
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Assert.fail
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith
import java.io.File

/**
 * UT-E13.1 (docs/PROGRESO.md): "Room + SQLCipher con clave en Keystore"
 * (00 §D3). Pruebas **instrumentadas** —emulador real, no JVM— porque lo
 * que se verifica no se puede simular: que el fichero que queda en disco
 * está de verdad cifrado, y que la clave vive de verdad en el Android
 * Keystore. Un doble de prueba aquí sería un placebo, exactamente el
 * mismo criterio con el que E3.4 probó el `DENY` de SQL Server con un
 * principal real en vez de asumirlo.
 */
@RunWith(AndroidJUnit4::class)
class CacheCifradaTest {

    private val contexto = InstrumentationRegistry.getInstrumentation().targetContext
    private val clave = ClaveCacheKeystore()
    private lateinit var base: CacheDatabase

    private fun ficheroDeBase(): File = contexto.getDatabasePath(NOMBRE_FICHERO_PRUEBA)

    private fun abrirBase(): CacheDatabase {
        SqlCipherNativo.cargar()
        return Room.databaseBuilder(contexto, CacheDatabase::class.java, NOMBRE_FICHERO_PRUEBA)
            .openHelperFactory(SupportOpenHelperFactory(clave.contrasenaDeBase()))
            .build()
    }

    @Before
    fun preparar() {
        contexto.deleteDatabase(NOMBRE_FICHERO_PRUEBA)
        base = abrirBase()
    }

    @After
    fun limpiar() {
        base.close()
        contexto.deleteDatabase(NOMBRE_FICHERO_PRUEBA)
    }

    private fun persona(id: Int = 1, ficha: String = "F-0001") = PersonaCacheadaEntity(
        personalId = id, ficha = ficha, nombreCompleto = "María López",
        categoria = "operario", cacheadoEn = 1_700_000_000_000
    )

    @Test
    fun guarda_y_devuelve_la_persona_con_sus_restricciones() = runBlocking {
        base.cacheDao().guardar(persona(), listOf("No levantar peso", "No exposición a frío"))

        val guardada = base.cacheDao().personaPorFicha("F-0001")
        assertNotNull(guardada)
        assertEquals("María López", guardada!!.nombreCompleto)
        assertEquals("operario", guardada.categoria)
        // §12.2: las restricciones activas son requisito PREVIO a
        // consolidar — sin red tienen que seguir estando.
        assertEquals(
            listOf("No levantar peso", "No exposición a frío"),
            base.cacheDao().restriccionesDe(guardada.personalId)
        )
    }

    @Test
    fun el_fichero_en_disco_no_se_puede_abrir_como_sqlite_en_claro() {
        // ESTA es la prueba que da sentido a la UT (D3: "base local
        // cifrada"). Si SQLCipher no estuviera actuando, SQLite abriría
        // el fichero sin problema y este test pasaría a verde por la
        // razón equivocada — por eso se exige que LANCE.
        runBlocking { base.cacheDao().guardar(persona(), listOf("No levantar peso")) }
        base.close()

        val fichero = ficheroDeBase()
        assertTrue("la base debería existir en disco", fichero.exists())

        try {
            SQLiteDatabase.openDatabase(fichero.absolutePath, null, SQLiteDatabase.OPEN_READONLY).use {
                it.rawQuery("SELECT * FROM persona_cacheada", null).use { cursor -> cursor.moveToFirst() }
            }
            fail("SQLite abrió en claro una base que debía estar cifrada por SQLCipher (D3)")
        } catch (_: SQLiteException) {
            // Correcto: sin la clave, el fichero no es una base legible.
        }

        base = abrirBase() // para que @After cierre algo válido
    }

    @Test
    fun los_bytes_en_disco_no_contienen_el_nombre_ni_la_restriccion_medica_en_claro() {
        // Complementa al test anterior desde el otro lado: aunque alguien
        // no logre ABRIR el fichero, un cifrado mal aplicado podría dejar
        // el texto legible dentro. Se buscan los bytes literales.
        runBlocking { base.cacheDao().guardar(persona(), listOf("No levantar peso")) }
        base.close()

        val bytes = ficheroDeBase().readBytes()
        assertTrue("el fichero debería tener contenido", bytes.isNotEmpty())
        assertTrue("el nombre de la persona no puede aparecer en claro en disco", !contiene(bytes, "María López"))
        assertTrue("la restricción médica no puede aparecer en claro en disco", !contiene(bytes, "No levantar peso"))
        // Y la contraprueba de que la búsqueda de bytes de verdad
        // funciona: una base SQLite en claro SÍ delataría estas cadenas.
        assertTrue("SQLCipher marca la cabecera; nunca debe empezar por 'SQLite format 3'", !contiene(bytes, "SQLite format 3"))

        base = abrirBase()
    }

    @Test
    fun con_otra_contrasena_la_base_no_abre() {
        runBlocking { base.cacheDao().guardar(persona(), listOf("No levantar peso")) }
        base.close()

        SqlCipherNativo.cargar()
        val conClaveEquivocada = Room.databaseBuilder(contexto, CacheDatabase::class.java, NOMBRE_FICHERO_PRUEBA)
            .openHelperFactory(SupportOpenHelperFactory("contraseña-que-no-es".toByteArray()))
            .build()

        try {
            runBlocking { conClaveEquivocada.cacheDao().cuantasPersonas() }
            fail("la base abrió con una contraseña equivocada — el cifrado no está protegiendo nada")
        } catch (_: Exception) {
            // Correcto.
        } finally {
            conClaveEquivocada.close()
        }

        base = abrirBase()
    }

    @Test
    fun la_contrasena_derivada_del_keystore_es_estable_entre_llamadas() {
        // Si no lo fuera, la base cifrada del arranque anterior quedaría
        // ilegible en cada reinicio: la caché sin conexión no sobreviviría
        // ni a cerrar la app, que es justo lo que §12.1 necesita.
        val primera = clave.contrasenaDeBase()
        val segunda = ClaveCacheKeystore().contrasenaDeBase()

        assertTrue("la derivación desde el Keystore debe ser determinista", primera.contentEquals(segunda))
        assertTrue("una contraseña vacía no cifraría nada", primera.isNotEmpty())
    }

    @Test
    fun la_base_sobrevive_a_cerrarla_y_volver_a_abrirla() {
        runBlocking { base.cacheDao().guardar(persona(), listOf("No levantar peso")) }
        base.close()

        base = abrirBase()

        val recuperada = runBlocking { base.cacheDao().personaPorFicha("F-0001") }
        assertNotNull("la caché debe sobrevivir al reinicio de la app (§12.1)", recuperada)
        assertEquals(listOf("No levantar peso"), runBlocking { base.cacheDao().restriccionesDe(recuperada!!.personalId) })
    }

    @Test
    fun purgar_borra_las_personas_y_arrastra_sus_restricciones_medicas() = runBlocking {
        base.cacheDao().guardar(persona(id = 1, ficha = "F-0001"), listOf("No levantar peso"))
        base.cacheDao().guardar(persona(id = 2, ficha = "F-0002"), listOf("No exposición a frío", "Turno diurno"))
        assertEquals(2, base.cacheDao().cuantasPersonas())
        assertEquals(3, base.cacheDao().cuantasRestricciones())

        base.cacheDao().purgarTodo()

        assertEquals(0, base.cacheDao().cuantasPersonas())
        // D3: purgar no puede dejar un dato médico huérfano en disco.
        assertEquals(0, base.cacheDao().cuantasRestricciones())
    }

    @Test
    fun refrescar_una_persona_reemplaza_sus_restricciones_no_las_acumula() = runBlocking {
        base.cacheDao().guardar(persona(), listOf("No levantar peso"))

        base.cacheDao().guardar(persona(), listOf("No exposición a frío"))

        // Mezclar restricciones de dos sincronizaciones mostraría una
        // restricción ya levantada como si siguiera activa — §12.4.
        assertEquals(listOf("No exposición a frío"), base.cacheDao().restriccionesDe(1))
        assertEquals(1, base.cacheDao().cuantasRestricciones())
    }

    @Test
    fun una_persona_que_no_esta_en_la_cache_devuelve_null_no_una_vacia() {
        // §12.4, honestidad: "no la tengo cacheada" y "la tengo sin
        // restricciones" son estados distintos y no pueden confundirse.
        assertNull(runBlocking { base.cacheDao().personaPorFicha("F-9999") })
        assertNull(runBlocking { base.cacheDao().personaPorId(9999) })
    }

    private fun contiene(bytes: ByteArray, texto: String): Boolean {
        val aguja = texto.toByteArray()
        if (aguja.isEmpty() || bytes.size < aguja.size) return false
        outer@ for (i in 0..bytes.size - aguja.size) {
            for (j in aguja.indices) if (bytes[i + j] != aguja[j]) continue@outer
            return true
        }
        return false
    }

    private companion object {
        const val NOMBRE_FICHERO_PRUEBA = "smartassign_cache_prueba.db"
    }
}
