package com.smartassign.app.data.conectividad

import org.junit.Assert.assertEquals
import org.junit.Test

/** UT-E13.3 — el estado en sí, sin red real de por medio. */
class ConectividadRepositorioImplTest {

    @Test
    fun arranca_conectado_de_forma_optimista() {
        val repo = ConectividadRepositorioImpl()

        assertEquals(EstadoConectividad.Conectado, repo.estado.value)
    }

    @Test
    fun reportar_inalcanzable_pasa_a_sin_conexion() {
        val repo = ConectividadRepositorioImpl()

        repo.reportarInalcanzable()

        assertEquals(EstadoConectividad.SinConexion, repo.estado.value)
    }

    @Test
    fun reportar_alcanzado_despues_de_una_caida_vuelve_a_conectado() {
        val repo = ConectividadRepositorioImpl()
        repo.reportarInalcanzable()

        repo.reportarAlcanzado()

        assertEquals(EstadoConectividad.Conectado, repo.estado.value)
    }
}
