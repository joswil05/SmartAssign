package com.smartassign.app.data.cache

import android.security.keystore.KeyGenParameterSpec
import android.security.keystore.KeyProperties
import java.security.KeyStore
import javax.crypto.Cipher
import javax.crypto.KeyGenerator
import javax.crypto.SecretKey
import javax.crypto.spec.GCMParameterSpec
import javax.inject.Inject
import javax.inject.Singleton

private const val KEYSTORE = "AndroidKeyStore"
private const val ALIAS_CLAVE = "smartassign_cache_operativa"

// Constantes de derivación. Ninguna es secreta: el secreto es la clave AES
// que vive dentro del Keystore y que NUNCA sale de él (ver más abajo).
private const val LONGITUD_TAG_BITS = 128
private val SEMILLA_DERIVACION = "SmartAssign/cache-operativa/v1".toByteArray()
private val IV_DERIVACION = byteArrayOf(
    0x53, 0x6D, 0x61, 0x72, 0x74, 0x41, 0x73, 0x73, 0x69, 0x67, 0x6E, 0x31
)

/**
 * D3, literal: *"Base local cifrada, clave en Android Keystore. **Jamás**
 * en preferencias, ficheros planos ni logs."*
 *
 * La contraseña de SQLCipher **no se guarda en ningún sitio** — ni
 * siquiera cifrada en `EncryptedSharedPreferences`, que es donde viven
 * los tokens (05 §3.2) pero que sigue siendo "preferencias". Se **deriva**
 * en cada arranque cifrando una semilla constante con una clave AES que
 * el Android Keystore genera y guarda de forma no exportable: el material
 * de clave nunca cruza a la memoria de la app, solo el resultado de la
 * operación. Nada que persista en disco fuera del Keystore sirve para
 * abrir la base.
 *
 * Por qué la derivación es determinista (misma semilla, mismo IV, misma
 * salida — `setRandomizedEncryptionRequired(false)`): la contraseña tiene
 * que ser la MISMA en cada arranque o la base cifrada del arranque
 * anterior quedaría ilegible. El riesgo habitual de deshabilitar el IV
 * aleatorio —que cifrar dos veces el mismo texto claro delate que son
 * iguales— aquí no aplica: se cifra un único texto constante, una sola
 * vez por arranque, y el resultado nunca se transmite ni se persiste.
 *
 * Si el Keystore pierde la clave (restauración de fábrica, cambio de
 * bloqueo de pantalla en algunos fabricantes, desinstalación), la base
 * anterior se vuelve ilegible y hay que descartarla — es exactamente el
 * comportamiento deseado para una caché de datos médicos, no una pérdida
 * de datos: el servidor sigue siendo la fuente de verdad (§1.4).
 */
@Singleton
class ClaveCacheKeystore @Inject constructor() {

    /**
     * Contraseña de SQLCipher para esta instalación. Se pide en cada
     * apertura de la base; el llamador debe limpiarla en cuanto SQLCipher
     * la haya consumido — SQLCipher sobrescribe el array que recibe.
     */
    fun contrasenaDeBase(): ByteArray {
        val cipher = Cipher.getInstance("AES/GCM/NoPadding")
        cipher.init(Cipher.ENCRYPT_MODE, claveDelKeystore(), GCMParameterSpec(LONGITUD_TAG_BITS, IV_DERIVACION))
        return cipher.doFinal(SEMILLA_DERIVACION)
    }

    /**
     * Borra la clave del Keystore. Sin ella, el fichero cifrado que quede
     * en disco es ilegible para siempre — es la purga real de D3 ("al
     * cerrar sesión, al cerrar turno, al reasignar línea"): no depende de
     * que el borrado de filas haya llegado a tocar cada sector del disco.
     * Los disparadores concretos de esa purga son E13.2, no esta UT.
     */
    fun olvidarClave() {
        KeyStore.getInstance(KEYSTORE).apply { load(null) }.deleteEntry(ALIAS_CLAVE)
    }

    private fun claveDelKeystore(): SecretKey {
        val keystore = KeyStore.getInstance(KEYSTORE).apply { load(null) }
        (keystore.getEntry(ALIAS_CLAVE, null) as? KeyStore.SecretKeyEntry)?.let { return it.secretKey }

        val generador = KeyGenerator.getInstance(KeyProperties.KEY_ALGORITHM_AES, KEYSTORE)
        generador.init(
            KeyGenParameterSpec.Builder(ALIAS_CLAVE, KeyProperties.PURPOSE_ENCRYPT or KeyProperties.PURPOSE_DECRYPT)
                .setBlockModes(KeyProperties.BLOCK_MODE_GCM)
                .setEncryptionPaddings(KeyProperties.ENCRYPTION_PADDING_NONE)
                .setKeySize(256)
                // Ver el comentario de clase: la derivación tiene que ser
                // determinista para que la base sobreviva al reinicio.
                .setRandomizedEncryptionRequired(false)
                // A propósito SIN setUserAuthenticationRequired: la app
                // trabaja con el teléfono en la mano, con guantes, de pie
                // (§12.3) — exigir huella/PIN del sistema para cada
                // apertura de la caché rompería el requisito de que una
                // terminal sin red se comporte igual que una conectada
                // (§12.1). El PIN de aplicación (D6) es la barrera que sí
                // eligió el cliente.
                .build()
        )
        return generador.generateKey()
    }
}
