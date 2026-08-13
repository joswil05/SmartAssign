package com.smartassign.app.data.cache

import androidx.room.Dao
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.Query
import androidx.room.Transaction

@Dao
interface CacheDao {

    /**
     * Guarda persona + restricciones en una sola transacción: media
     * persona cacheada —con nombre pero sin sus restricciones médicas—
     * sería exactamente el fallo que §12.2 quiere impedir (consolidar un
     * registro sin ver las restricciones activas). `REPLACE` sobre la
     * persona borra en cascada sus restricciones viejas, así que
     * refrescar nunca deja mezcladas las de dos sincronizaciones.
     */
    @Transaction
    suspend fun guardar(persona: PersonaCacheadaEntity, restricciones: List<String>) {
        insertarPersona(persona)
        insertarRestricciones(restricciones.map { RestriccionCacheadaEntity(personalId = persona.personalId, descripcion = it) })
    }

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertarPersona(persona: PersonaCacheadaEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertarRestricciones(restricciones: List<RestriccionCacheadaEntity>)

    @Query("SELECT * FROM persona_cacheada WHERE ficha = :ficha")
    suspend fun personaPorFicha(ficha: String): PersonaCacheadaEntity?

    @Query("SELECT * FROM persona_cacheada WHERE personalId = :personalId")
    suspend fun personaPorId(personalId: Int): PersonaCacheadaEntity?

    @Query("SELECT descripcion FROM restriccion_cacheada WHERE personalId = :personalId")
    suspend fun restriccionesDe(personalId: Int): List<String>

    @Query("SELECT COUNT(*) FROM persona_cacheada")
    suspend fun cuantasPersonas(): Int

    @Query("SELECT COUNT(*) FROM restriccion_cacheada")
    suspend fun cuantasRestricciones(): Int

    /**
     * Purga de D3 ("al cerrar sesión, al cerrar turno, al reasignar
     * línea, y por inactividad configurable"). Borra solo las personas:
     * las restricciones caen con ellas por `ON DELETE CASCADE`, así que
     * es imposible dejar un dato médico huérfano por olvidar una línea.
     * QUIÉN dispara esta purga y CUÁNDO es E13.2 — aquí solo existe la
     * operación.
     */
    @Query("DELETE FROM persona_cacheada")
    suspend fun purgarTodo()

    // ═══ Alcance de la caché (E13.2, 00 §D3) ═══

    @Query("SELECT * FROM alcance_cache WHERE id = 1")
    suspend fun alcance(): AlcanceCacheEntity?

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun guardarAlcance(alcance: AlcanceCacheEntity)

    @Query("DELETE FROM alcance_cache")
    suspend fun borrarAlcance()

    /**
     * Purga total: personas (con sus restricciones por cascada) **y** el
     * alcance. D3 la exige "al cerrar sesión, al cerrar turno, al
     * reasignar línea, y por inactividad configurable". Borrar el
     * alcance junto con los datos importa: una caché vacía pero todavía
     * "abierta" a una línea aceptaría escrituras nuevas sin que nadie
     * volviera a declarar para quién.
     */
    @Transaction
    suspend fun purgarTodoYAlcance() {
        purgarTodo()
        borrarAlcance()
    }
}
