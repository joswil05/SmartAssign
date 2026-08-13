package com.smartassign.app.data.version

import com.smartassign.app.data.red.VersionActualResponse
import org.junit.Assert.assertEquals
import org.junit.Test

/** UT-E14.6 (00 §F3, literal): la comparación pura que decide entre bloquear, ofrecer o no hacer nada. */
class ResultadoVersionTest {

    private val descargaUrl = "https://servidor/api/version-app/apk"

    private fun servidor(versionNombre: String, versionCodigo: Int, versionMinimaApi: Int) =
        VersionActualResponse(versionNombre, versionCodigo, versionMinimaApi, notas = null, publicadaEn = "2026-08-13T00:00:00Z")

    @Test
    fun por_debajo_del_minimo_bloquea() {
        val resultado = evaluarVersion(codigoPropio = 5, servidor("2.0.0", versionCodigo = 10, versionMinimaApi = 8), descargaUrl)

        assertEquals(ResultadoVersion.Bloqueada("2.0.0", descargaUrl), resultado)
    }

    @Test
    fun por_debajo_de_la_ultima_pero_por_encima_del_minimo_ofrece_sin_bloquear() {
        val resultado = evaluarVersion(codigoPropio = 8, servidor("2.0.0", versionCodigo = 10, versionMinimaApi = 8), descargaUrl)

        assertEquals(ResultadoVersion.ActualizacionDisponible("2.0.0", descargaUrl), resultado)
    }

    @Test
    fun al_dia_no_hace_nada() {
        val resultado = evaluarVersion(codigoPropio = 10, servidor("2.0.0", versionCodigo = 10, versionMinimaApi = 8), descargaUrl)

        assertEquals(ResultadoVersion.Compatible, resultado)
    }

    @Test
    fun exactamente_en_el_minimo_no_bloquea_el_limite_es_inclusive() {
        // 00 §F3: "por debajo de ese mínimo" — igual al mínimo SÍ es compatible.
        val resultado = evaluarVersion(codigoPropio = 8, servidor("2.0.0", versionCodigo = 8, versionMinimaApi = 8), descargaUrl)

        assertEquals(ResultadoVersion.Compatible, resultado)
    }

    @Test
    fun una_version_mas_nueva_que_la_ultima_publicada_tampoco_hace_nada() {
        // Convivencia de versiones (Anexo §3) — un dispositivo ya
        // actualizado a algo más nuevo que lo publicado no debe verse
        // como "desactualizado" solo porque el registro del servidor
        // todavía no se refrescó a esa cifra.
        val resultado = evaluarVersion(codigoPropio = 15, servidor("2.0.0", versionCodigo = 10, versionMinimaApi = 8), descargaUrl)

        assertEquals(ResultadoVersion.Compatible, resultado)
    }
}
