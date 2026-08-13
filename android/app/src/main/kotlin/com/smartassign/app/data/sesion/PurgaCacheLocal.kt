package com.smartassign.app.data.sesion

/**
 * Lo único que el ciclo de sesión necesita saber de la caché cifrada:
 * que se puede vaciar. 00 §D3 exige purgarla *"al cerrar sesión"*, y
 * hasta E13.2 `cerrarSesion()` solo limpiaba `EncryptedSharedPreferences`
 * — los datos médicos cacheados sobrevivían al logout en un teléfono que
 * D6 trata como **compartido por línea**, es decir, el siguiente usuario
 * los heredaba.
 *
 * Es un puerto y no una dependencia directa a `CachePersonalRepositorio`
 * para que la sesión no arrastre Room ni SQLCipher: las pruebas de JVM de
 * `SesionRepositorioIntegrationTest` corren sin emulador y no podrían
 * construir una base cifrada.
 */
interface PurgaCacheLocal {
    suspend fun purgar()
}
