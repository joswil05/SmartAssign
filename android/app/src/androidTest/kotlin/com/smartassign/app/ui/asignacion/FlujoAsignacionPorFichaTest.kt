package com.smartassign.app.ui.asignacion

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import com.smartassign.app.data.asignacion.FakeAsignacionRepositorio
import com.smartassign.app.data.asignacion.ResultadoAsignar
import com.smartassign.app.data.asignacion.ResultadoSugerencia
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.personal.FakePersonalRepositorio
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.personal.ResultadoPersonal
import com.smartassign.app.ui.confirmacion.ConfirmacionIdentidadViewModel
import org.junit.Rule
import org.junit.Test

/**
 * La integración completa que falta antes de PC-3 (07 §6): ficha
 * escaneada → identidad (E6.6) → sugerencia (E6.7) → asignación (E6.8).
 */
class FlujoAsignacionPorFichaTest {

    @get:Rule
    val compose = createComposeRule()

    private val persona = PersonaConfirmacion(
        personalId = 42, nombreCompleto = "María López Hernández", ficha = "4821",
        categoria = "operario", restriccionesMedicas = emptyList()
    )

    private fun puesto(id: Int, codigo: String, tipo: String) = PuestoMalla(
        id = id, codigo = codigo, nombrePuesto = codigo, tipo = tipo, situacion = "descubierto",
        ocupante = null, indicadorMedico = 0, microCopia = "micro-copia de prueba"
    )

    @Test
    fun sugerencia_lista_muestra_el_modal_de_identidad_con_el_destino_sugerido() {
        val personalRepo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(persona) }
        val asignacionRepo = FakeAsignacionRepositorio().apply {
            resultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 7, nivel = 1)
        }

        compose.setContent {
            FlujoAsignacionPorFicha(
                ficha = "4821",
                puestosDeLinea = listOf(puesto(7, "L4-R03", "rotativo")),
                onTerminado = {},
                viewModel = FlujoAsignacionViewModel(personalRepo, asignacionRepo),
                viewModelConfirmacion = ConfirmacionIdentidadViewModel(personalRepo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-identidad").assertExists()
        compose.onNodeWithText("Destino: L4-R03 · Rotativo").assertExists()
    }

    @Test
    fun confirmar_la_identidad_consolida_la_asignacion_y_termina_el_flujo() {
        val personalRepo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(persona) }
        val asignacionRepo = FakeAsignacionRepositorio().apply {
            resultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 7, nivel = 1)
            resultadoAsignar = ResultadoAsignar.Ok(asignacionId = 555L)
        }
        var terminado = false

        compose.setContent {
            FlujoAsignacionPorFicha(
                ficha = "4821",
                puestosDeLinea = listOf(puesto(7, "L4-R03", "rotativo")),
                onTerminado = { terminado = true },
                viewModel = FlujoAsignacionViewModel(personalRepo, asignacionRepo),
                viewModelConfirmacion = ConfirmacionIdentidadViewModel(personalRepo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-confirmar").performClick()
        compose.waitForIdle()

        assert(terminado)
        assert(asignacionRepo.ultimaPeticionAsignar?.puestoId == 7)
        assert(asignacionRepo.ultimaPeticionAsignar?.personalId == 42)
    }

    @Test
    fun sin_puestos_libres_muestra_el_mensaje_real_del_servidor_y_cerrar_termina_el_flujo() {
        val personalRepo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(persona) }
        val asignacionRepo = FakeAsignacionRepositorio().apply {
            resultadoSugerencia = ResultadoSugerencia.SinSugerencia(
                codigo = "SIN_PUESTOS_LIBRES",
                mensaje = "No hay puestos rotativos libres compatibles en L4."
            )
        }
        var terminado = false

        compose.setContent {
            FlujoAsignacionPorFicha(
                ficha = "4821",
                puestosDeLinea = emptyList(),
                onTerminado = { terminado = true },
                viewModel = FlujoAsignacionViewModel(personalRepo, asignacionRepo),
                viewModelConfirmacion = ConfirmacionIdentidadViewModel(personalRepo)
            )
        }

        compose.onNodeWithTag("flujo-asignacion-error").assertExists()
        compose.onNodeWithText("No hay puestos rotativos libres compatibles en L4.").assertExists()

        compose.onNodeWithTag("flujo-asignacion-error-cerrar").performClick()

        assert(terminado)
    }

    @Test
    fun rechazo_nominal_al_confirmar_se_muestra_tal_cual_lo_devuelve_el_servidor() {
        // 00 §B1: mensaje nominal real ("acaba de ser registrada..."), nunca genérico.
        val personalRepo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(persona) }
        val asignacionRepo = FakeAsignacionRepositorio().apply {
            resultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 7, nivel = 1)
            resultadoAsignar = ResultadoAsignar.Rechazado(
                codigo = "PUESTO_OCUPADO",
                mensaje = "María López Hernández acaba de ser registrada en L4 · Puesto 3 por otro supervisor."
            )
        }

        compose.setContent {
            FlujoAsignacionPorFicha(
                ficha = "4821",
                puestosDeLinea = listOf(puesto(7, "L4-R03", "rotativo")),
                onTerminado = {},
                viewModel = FlujoAsignacionViewModel(personalRepo, asignacionRepo),
                viewModelConfirmacion = ConfirmacionIdentidadViewModel(personalRepo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-confirmar").performClick()
        compose.waitForIdle()

        compose.onNodeWithTag("flujo-asignacion-error").assertExists()
        compose.onNodeWithText("María López Hernández acaba de ser registrada en L4 · Puesto 3 por otro supervisor.").assertExists()
    }
}
