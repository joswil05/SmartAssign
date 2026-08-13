package com.smartassign.app.ui.malla

import androidx.compose.ui.test.assertHeightIsAtLeast
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import com.smartassign.app.data.malla.FakeMallaRepositorio
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.malla.ResultadoMalla
import com.smartassign.app.data.red.LineaVigenteResponse
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.ui.theme.TouchTarget
import org.junit.Rule
import org.junit.Test

/**
 * UT-E14.5 (docs/PROGRESO.md): "48 dp, AAA, escala de grises" — 03 §5.1,
 * literal: "48 dp de piso, 64 dp en la acción primaria (A11)". Composición
 * real (`connectedDebugAndroidTest`), no valores de constante: mismo
 * criterio que `TemaAplicadoContrasteTest` (E13.1) — un token correcto no
 * prueba nada si el componente real no lo aplica.
 *
 * **Dos bugs reales encontrados por esta prueba, antes de tocar nada**:
 * el botón flotante "escanear gafete" no traía ningún alto explícito —
 * usaba el tamaño de FAB por defecto de Material3 (56 dp), por debajo de
 * los 64 dp que le corresponden como acción primaria de la pantalla
 * central de la app (03 §3.1: "el corazón de la app del supervisor").
 * Y la fila de colapso "N puestos no requeridos" era un `Text` con
 * `.clickable` y solo `Spacing.sm` (8 dp) de relleno vertical — sin
 * ningún alto mínimo, por debajo incluso del piso absoluto de 48 dp.
 * Todo el resto de botones de la app (`LoginScreen`, `PinScreen`,
 * `ModalConfirmacionIdentidad`, `AltaDispositivoScreen`,
 * `EscaneoGafeteScreen`) ya aplicaba `TouchTarget` explícitamente — estos
 * dos quedaron atrás.
 */
class MallaLineaZonasDeToqueTest {

    @get:Rule
    val compose = createComposeRule()

    private fun puesto(id: Int, situacion: String) = PuestoMalla(
        id = id, codigo = "L4-A0$id", nombrePuesto = "Puesto $id", tipo = "fijo", situacion = situacion,
        ocupante = null, indicadorMedico = 0, microCopia = "micro-copia de prueba"
    )

    private fun sesionConLinea(id: Int = 4) = FakeSesionRepositorio().apply {
        quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(id, "L$id", "Línea $id", esBolson = false))
    }

    @Test
    fun el_boton_de_escanear_gafete_es_la_accion_primaria_y_mide_64dp() {
        val malla = FakeMallaRepositorio().apply {
            resultado = ResultadoMalla.Ok(listOf(puesto(1, "libre")))
        }

        compose.setContent { MallaLineaScreen(viewModel = MallaLineaViewModel(sesionConLinea(), malla)) }

        compose.onNodeWithTag("malla-boton-escanear-gafete")
            .assertHeightIsAtLeast(TouchTarget.accionPrimaria)
    }

    @Test
    fun la_fila_de_colapso_de_fuera_de_operacion_cumple_el_piso_absoluto_de_48dp() {
        val malla = FakeMallaRepositorio().apply {
            resultado = ResultadoMalla.Ok(listOf(puesto(1, "fuera_de_operacion")))
        }

        compose.setContent { MallaLineaScreen(viewModel = MallaLineaViewModel(sesionConLinea(), malla)) }

        compose.onNodeWithTag("malla-fuera-de-operacion-colapso")
            .assertHeightIsAtLeast(TouchTarget.minimo)
    }
}
