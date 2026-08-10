package com.smartassign.app.ui.confirmacion

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.personal.FakePersonalRepositorio
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.personal.ResultadoPersonal
import com.smartassign.app.ui.estado.EstadoPantalla
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** 03 §3.3 / 00 §E1 §12.2: resolución de la ficha escaneada, antes de que el modal pueda confirmar nada. */
class ConfirmacionIdentidadViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val persona = PersonaConfirmacion(
        personalId = 1, nombreCompleto = "María López Hernández", ficha = "4821",
        categoria = "operario", restriccionesMedicas = listOf("No levantar carga superior a 10 kg")
    )

    @Test
    fun empieza_cargando() {
        val vm = ConfirmacionIdentidadViewModel(FakePersonalRepositorio())
        assertTrue(vm.estado.value is EstadoPantalla.Cargando)
    }

    @Test
    fun una_ficha_resuelta_pasa_a_listo_con_la_persona() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(persona) }
        val vm = ConfirmacionIdentidadViewModel(repo)

        vm.cargar("4821")

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Listo)
        assertEquals(persona, (estado as EstadoPantalla.Listo).datos)
    }

    @Test
    fun una_ficha_sin_dueno_pasa_a_error_con_causa_y_accion_concretas() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.NoEncontrado }
        val vm = ConfirmacionIdentidadViewModel(repo)

        vm.cargar("no-existe")

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Error)
        val error = estado as EstadoPantalla.Error
        assertTrue(error.causa.contains("no-existe"))
        assertTrue(error.accionSugerida.isNotBlank())
    }

    @Test
    fun sin_conexion_tambien_pasa_a_error_no_se_queda_cargando_para_siempre() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.SinConexion }
        val vm = ConfirmacionIdentidadViewModel(repo)

        vm.cargar("4821")

        assertTrue(vm.estado.value is EstadoPantalla.Error)
    }

    @Test
    fun cargar_de_nuevo_vuelve_a_mostrar_cargando_antes_del_resultado() {
        // No se puede observar el estado intermedio con un fake síncrono,
        // pero si cargar() no reinicia el estado, un reintento tras un
        // error quedaría atascado mostrando el error viejo mientras llega
        // la respuesta nueva — lo que sí se puede probar es que el
        // resultado final refleja la segunda llamada, no la primera.
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.NoEncontrado }
        val vm = ConfirmacionIdentidadViewModel(repo)
        vm.cargar("ficha-mala")
        assertTrue(vm.estado.value is EstadoPantalla.Error)

        repo.resultado = ResultadoPersonal.Ok(persona)
        vm.cargar("4821")

        assertTrue(vm.estado.value is EstadoPantalla.Listo)
    }
}
