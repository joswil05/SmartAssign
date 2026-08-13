package com.smartassign.app.data.cache

import androidx.room.Database
import androidx.room.RoomDatabase

/**
 * La caché operativa sin conexión (D3, 05 §3.2). Nunca se abre sin
 * SQLCipher — ver `CacheModule`, que es el único sitio que la construye.
 */
@Database(
    entities = [PersonaCacheadaEntity::class, RestriccionCacheadaEntity::class],
    version = 1,
    exportSchema = false
)
abstract class CacheDatabase : RoomDatabase() {
    abstract fun cacheDao(): CacheDao

    companion object {
        const val NOMBRE_FICHERO = "smartassign_cache.db"
    }
}
