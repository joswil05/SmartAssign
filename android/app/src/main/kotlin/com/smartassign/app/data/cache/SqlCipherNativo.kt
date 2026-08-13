package com.smartassign.app.data.cache

/**
 * SQLCipher es una librería nativa: hay que cargarla antes de abrir
 * ninguna base. Se centraliza aquí —y no en `Application.onCreate`—
 * para que cualquier ruta que abra la caché (la app real vía
 * `CacheModule`, o una prueba instrumentada que construya la base a
 * mano) pase por el mismo sitio y no dependa de que alguien recuerde
 * inicializarla. `System.loadLibrary` es idempotente: llamarla dos veces
 * no cuesta nada.
 */
object SqlCipherNativo {
    fun cargar() {
        System.loadLibrary("sqlcipher")
    }
}
