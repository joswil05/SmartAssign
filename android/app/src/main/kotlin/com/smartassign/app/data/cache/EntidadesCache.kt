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
 * Coordinador nunca cachea restricciones médicas) es la regla de D3 que
 * construye **E13.2** — esta UT solo levanta el almacén cifrado.
 */
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
