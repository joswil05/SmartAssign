package com.smartassign.app.data.cache

import android.content.Context
import androidx.room.Room
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import net.zetetic.database.sqlcipher.SupportOpenHelperFactory
import javax.inject.Singleton

/**
 * El ÚNICO sitio donde se construye la caché — y siempre con
 * `SupportOpenHelperFactory` de SQLCipher (D3: "base local cifrada").
 * Que la construcción viva en un solo lugar es lo que hace posible la
 * garantía: no hay ninguna otra ruta por la que alguien pueda abrir
 * `smartassign_cache.db` en claro "para depurar".
 */
@Module
@InstallIn(SingletonComponent::class)
object CacheModule {

    @Provides
    @Singleton
    fun cacheDatabase(
        @ApplicationContext contexto: Context,
        clave: ClaveCacheKeystore
    ): CacheDatabase {
        SqlCipherNativo.cargar()
        // SQLCipher borra el array de la contraseña en cuanto lo consume,
        // así que el secreto no queda flotando en el heap de la app.
        val contrasena = clave.contrasenaDeBase()
        return Room.databaseBuilder(contexto, CacheDatabase::class.java, CacheDatabase.NOMBRE_FICHERO)
            .openHelperFactory(SupportOpenHelperFactory(contrasena))
            .build()
    }

    @Provides
    fun cacheDao(base: CacheDatabase): CacheDao = base.cacheDao()
}
