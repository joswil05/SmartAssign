package com.smartassign.app.ui.confirmacion

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import com.smartassign.app.data.personal.FakePersonalRepositorio
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.personal.ResultadoPersonal
import org.junit.Rule
import org.junit.Test

/** 03 §3.3 — "componente de seguridad", diseño no negociable. */
class ModalConfirmacionIdentidadTest {

    @get:Rule
    val compose = createComposeRule()

    private val personaSinRestricciones = PersonaConfirmacion(
        personalId = 1, nombreCompleto = "María López Hernández", ficha = "4821",
        categoria = "operario", restriccionesMedicas = emptyList()
    )

    private val personaConRestricciones = personaSinRestricciones.copy(
        restriccionesMedicas = listOf("No levantar carga superior a 10 kg", "No bipedestación prolongada")
    )

    @Test
    fun muestra_nombre_ficha_categoria_y_destino() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(personaSinRestricciones) }

        compose.setContent {
            ModalConfirmacionIdentidad(
                ficha = "4821", destinoPuesto = "Puesto 3", destinoTipo = "Rotativo",
                onConfirmar = {}, onCancelar = {},
                viewModel = ConfirmacionIdentidadViewModel(repo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-nombre").assertExists()
        compose.onNodeWithText("María López Hernández").assertExists()
        compose.onNodeWithText("Ficha 4821").assertExists()
        compose.onNodeWithText("Operario").assertExists()
        compose.onNodeWithText("Destino: Puesto 3 · Rotativo").assertExists()
    }

    @Test
    fun sin_restricciones_el_bloque_medico_afirma_la_ausencia_no_la_omite() {
        // Regla 2 del §3.3 (fuente §12.4): la ausencia se afirma, no se omite.
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(personaSinRestricciones) }

        compose.setContent {
            ModalConfirmacionIdentidad(
                ficha = "4821", destinoPuesto = "Puesto 3", destinoTipo = "Rotativo",
                onConfirmar = {}, onCancelar = {},
                viewModel = ConfirmacionIdentidadViewModel(repo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-bloque-medico").assertExists()
        compose.onNodeWithText("Sin restricciones médicas registradas").assertExists()
    }

    @Test
    fun con_restricciones_las_lista_todas_explicitas_nunca_colapsadas() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(personaConRestricciones) }

        compose.setContent {
            ModalConfirmacionIdentidad(
                ficha = "4821", destinoPuesto = "Puesto 3", destinoTipo = "Rotativo",
                onConfirmar = {}, onCancelar = {},
                viewModel = ConfirmacionIdentidadViewModel(repo)
            )
        }

        compose.onNodeWithText("· No levantar carga superior a 10 kg").assertExists()
        compose.onNodeWithText("· No bipedestación prolongada").assertExists()
    }

    @Test
    fun confirmar_entrega_la_persona_resuelta_no_solo_la_ficha_escaneada() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.Ok(personaSinRestricciones) }
        var confirmada: PersonaConfirmacion? = null

        compose.setContent {
            ModalConfirmacionIdentidad(
                ficha = "4821", destinoPuesto = "Puesto 3", destinoTipo = "Rotativo",
                onConfirmar = { confirmada = it }, onCancelar = {},
                viewModel = ConfirmacionIdentidadViewModel(repo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-confirmar").performClick()

        assert(confirmada == personaSinRestricciones)
    }

    @Test
    fun cancelar_esta_disponible_incluso_cuando_la_ficha_no_se_pudo_resolver() {
        val repo = FakePersonalRepositorio().apply { resultado = ResultadoPersonal.NoEncontrado }
        var cancelado = false

        compose.setContent {
            ModalConfirmacionIdentidad(
                ficha = "ficha-inexistente", destinoPuesto = "Puesto 3", destinoTipo = "Rotativo",
                onConfirmar = {}, onCancelar = { cancelado = true },
                viewModel = ConfirmacionIdentidadViewModel(repo)
            )
        }

        compose.onNodeWithTag("modal-confirmacion-cancelar").performClick()

        assert(cancelado)
    }
}
