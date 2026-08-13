package com.smartassign.app.ui.frescura

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.conectividad.ConectividadRepositorio
import com.smartassign.app.data.conectividad.EstadoConectividad
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** UT-E13.4 — el ViewModel traduce EstadoConectividad a "¿se muestra el banner?", sin más lógica propia. */
class BannerSinSincronizarViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private class FakeConectividadRepositorio(inicial: EstadoConectividad) : ConectividadRepositorio {
        private val _estado = MutableStateFlow(inicial)
        override val estado: StateFlow<EstadoConectividad> = _estado
        override fun reportarAlcanzado() { _estado.value = EstadoConectividad.Conectado }
        override fun reportarInalcanzable() { _estado.value = EstadoConectividad.SinConexion }
    }

    @Test
    fun conectado_no_muestra_el_banner() {
        val vm = BannerSinSincronizarViewModel(FakeConectividadRepositorio(EstadoConectividad.Conectado))

        assertFalse(vm.mostrarBanner.value)
    }

    @Test
    fun sin_conexion_muestra_el_banner() {
        val vm = BannerSinSincronizarViewModel(FakeConectividadRepositorio(EstadoConectividad.SinConexion))

        assertTrue(vm.mostrarBanner.value)
    }

    @Test
    fun reacciona_cuando_la_conectividad_cambia_en_vivo() {
        val repo = FakeConectividadRepositorio(EstadoConectividad.Conectado)
        val vm = BannerSinSincronizarViewModel(repo)
        assertFalse(vm.mostrarBanner.value)

        repo.reportarInalcanzable()
        assertTrue("el banner debe encenderse solo, sin recrear el ViewModel", vm.mostrarBanner.value)

        repo.reportarAlcanzado()
        assertFalse("y apagarse solo al recuperar la red", vm.mostrarBanner.value)
    }
}
