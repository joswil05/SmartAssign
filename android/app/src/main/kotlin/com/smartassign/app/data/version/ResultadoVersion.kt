package com.smartassign.app.data.version

import com.smartassign.app.data.red.VersionActualResponse

/**
 * UT-E14.6 (00 §F3, literal): "la app comprueba la versión al iniciar
 * sesión y ofrece actualizar dentro de la app... la app solo se bloquea
 * si su código de versión queda por debajo de [version_minima_api]; en
 * cualquier otro caso, se ofrece la actualización pero no se impone."
 */
sealed interface ResultadoVersion {
    /** Ninguna acción — el código propio ya cumple el mínimo y no hay versión más nueva que ofrecer. */
    data object Compatible : ResultadoVersion

    /** No bloquea — hay una versión más nueva, se ofrece dentro de la app. */
    data class ActualizacionDisponible(val versionNombre: String, val descargaUrl: String) : ResultadoVersion

    /** Bloquea — por debajo del mínimo que el servidor exige. */
    data class Bloqueada(val versionNombre: String, val descargaUrl: String) : ResultadoVersion

    /** Sin conexión, o el servidor todavía no publicó ninguna versión (§1.3) — nunca bloquea por falta de dato. */
    data object SinDatoDelServidor : ResultadoVersion
}

/**
 * Pura, testable sin red — la comparación de números que decide entre
 * las tres formas reales de <see cref="ResultadoVersion"/>.
 */
fun evaluarVersion(codigoPropio: Int, servidor: VersionActualResponse, descargaUrl: String): ResultadoVersion = when {
    codigoPropio < servidor.versionMinimaApi -> ResultadoVersion.Bloqueada(servidor.versionNombre, descargaUrl)
    codigoPropio < servidor.versionCodigo -> ResultadoVersion.ActualizacionDisponible(servidor.versionNombre, descargaUrl)
    else -> ResultadoVersion.Compatible
}
