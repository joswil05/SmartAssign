package com.smartassign.app.data.cache

import androidx.room.Entity
import androidx.room.ForeignKey
import androidx.room.Index
import androidx.room.PrimaryKey

/**
 * La persona tal como §12.2 exige mostrarla **antes de consolidar
 * cualquier registro**: "nombre completo, el número de ficha, la
 * categoría y las restricciones médicas activas de forma explícita".
 * Espejo exacto de `PersonaConfirmacion` (E6.5) — el mismo conjunto de
 * campos que el endpoint real ya devuelve, ni uno inventado (R2).
 *
 * `cacheadoEn` es ALMACENAMIENTO para el sello de frescura de D4 ("Datos
 * de hace N min"); la presentación —el sello discreto, el banner
 * permanente, la degradación visual— es E13.4, no esta UT. Se declara la
 * columna ahora por el mismo criterio que `Puesto.PerfilRequerido` en
 * E3.1: quien escribe necesita dónde escribir, y añadirla después
 * costaría una migración de Room evitable.
 *
 * El ALCANCE de lo que puede entrar aquí (solo su línea + los
 * físicamente presentes en ella, **nunca el padrón completo**, y el
 * Coordinador nunca cachea restricciones médicas) lo impone
 * <see cref="AlcanceCacheEntity"/> + `CachePersonalRepositorio` (E13.2).
 */
/**
 * UT-E13.2 — el alcance al que pertenece TODA la caché. Fila única
 * (`id = 1`), no una columna por persona, y eso es deliberado: si el
 * alcance fuera por fila, cachear a alguien de otra línea sería un error
 * posible que alguien tendría que acordarse de no cometer. Siendo del
 * almacén entero, la pregunta *"¿puede colarse el padrón completo?"*
 * tiene una respuesta estructural: no hay dónde ponerlo — la caché sirve
 * a una línea y a un rol, y si cualquiera de los dos cambia se purga
 * entera antes de escribir nada (00 §D3, "purga... al reasignar línea").
 *
 * `rol` se guarda junto a la línea porque D3 le da al Coordinador una
 * regla propia: su dispositivo **no cachea restricciones médicas**.
 */
@Entity(tableName = "alcance_cache")
data class AlcanceCacheEntity(
    @PrimaryKey val id: Int = FILA_UNICA,
    val rol: String,
    val lineaId: Int?,
    val abiertoEn: Long
) {
    companion object {
        const val FILA_UNICA = 1
    }
}

@Entity(tableName = "persona_cacheada")
data class PersonaCacheadaEntity(
    @PrimaryKey val personalId: Int,
    val ficha: String,
    val nombreCompleto: String,
    val categoria: String,
    val cacheadoEn: Long
)

/**
 * Tabla hija porque `PersonaConfirmacion.restriccionesMedicas` es una
 * lista — Room no guarda listas sin un conversor, y una tabla real
 * permite además borrar las restricciones de una persona sin tocar su
 * fila (`onDelete = CASCADE`: purgar la persona purga sus datos médicos,
 * nunca los deja huérfanos en disco).
 */
@Entity(
    tableName = "restriccion_cacheada",
    foreignKeys = [
        ForeignKey(
            entity = PersonaCacheadaEntity::class,
            parentColumns = ["personalId"],
            childColumns = ["personalId"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [Index("personalId")]
)
data class RestriccionCacheadaEntity(
    @PrimaryKey(autoGenerate = true) val id: Long = 0,
    val personalId: Int,
    val descripcion: String
)
