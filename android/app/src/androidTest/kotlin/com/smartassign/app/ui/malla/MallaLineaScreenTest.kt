package com.smartassign.app.ui.malla

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import androidx.compose.ui.test.performClick
import com.smartassign.app.data.malla.FakeMallaRepositorio
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.malla.ResultadoMalla
import com.smartassign.app.data.red.LineaVigenteResponse
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import org.junit.Rule
import org.junit.Test

/** 03 §4.3: fijos primero, rotativos después, fuera de operación colapsado al final. */
class MallaLineaScreenTest {

    @get:Rule
    val compose = createComposeRule()

    private fun puesto(id: Int, codigo: String, tipo: String, situacion: String) = PuestoMalla(
        id = id, codigo = codigo, nombrePuesto = codigo, tipo = tipo, situacion = situacion,
        ocupante = null, indicadorMedico = 0, microCopia = "micro-copia de prueba"
    )

    private fun sesionConLinea(id: Int = 4) = FakeSesionRepositorio().apply {
        quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(id, "L$id", "Línea $id", esBolson = false))
    }

    @Test
    fun los_fuera_de_operacion_empiezan_colapsados_y_se_expanden_al_tocar() {
        val malla = FakeMallaRepositorio().apply {
            resultado = ResultadoMalla.Ok(listOf(
                puesto(1, "L4-A01", "fijo", "libre"),
                puesto(2, "L4-A02", "fijo", "fuera_de_operacion"),
                puesto(3, "L4-A03", "fijo", "fuera_de_operacion"),
            ))
        }

        compose.setContent { MallaLineaScreen(viewModel = MallaLineaViewModel(sesionConLinea(), malla)) }

        compose.onNodeWithTag("tarjeta-puesto-1").assertExists()
        compose.onNodeWithTag("tarjeta-puesto-2").assertDoesNotExist()
        compose.onNodeWithText("2 puestos no requeridos por el SKU de hoy").assertExists()

        compose.onNodeWithTag("malla-fuera-de-operacion-colapso").performClick()

        compose.onNodeWithTag("tarjeta-puesto-2").assertExists()
        compose.onNodeWithTag("tarjeta-puesto-3").assertExists()
    }

    @Test
    fun los_fijos_aparecen_en_su_propio_grupo_antes_que_los_rotativos() {
        val malla = FakeMallaRepositorio().apply {
            resultado = ResultadoMalla.Ok(listOf(
                puesto(1, "L4-A01", "fijo", "libre"),
                puesto(2, "L4-R01", "rotativo", "descubierto"),
            ))
        }

        compose.setContent { MallaLineaScreen(viewModel = MallaLineaViewModel(sesionConLinea(), malla)) }

        compose.onNodeWithText("PUESTOS FIJOS").assertExists()
        compose.onNodeWithText("PUESTOS ROTATIVOS").assertExists()
    }

    @Test
    fun lista_vacia_muestra_el_vacio_legitimo_no_una_pantalla_en_blanco() {
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.Ok(emptyList()) }

        compose.setContent { MallaLineaScreen(viewModel = MallaLineaViewModel(sesionConLinea(), malla)) }

        compose.onNodeWithText("No hay puestos activos en esta línea.").assertExists()
    }
}
