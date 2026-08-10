package com.smartassign.app.ui.malla

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.malla.FakeMallaRepositorio
import com.smartassign.app.data.malla.OcupantePuesto
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.malla.ResultadoMalla
import com.smartassign.app.data.red.LineaVigenteResponse
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.ui.estado.EstadoPantalla
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

class MallaLineaViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private fun puesto(id: Int = 1, situacion: String = "libre") = PuestoMalla(
        id = id, codigo = "L4-A0$id", nombrePuesto = "Puesto $id", tipo = "fijo",
        situacion = situacion, ocupante = null, indicadorMedico = 0, microCopia = "Esperando el arranque del turno"
    )

    @Test
    fun resuelve_la_linea_desde_quienSoy_y_consulta_esa_linea_exacta() {
        val sesion = FakeSesionRepositorio().apply {
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.Ok(listOf(puesto())) }

        MallaLineaViewModel(sesion, malla)

        assertEquals(4, malla.ultimaLineaIdConsultada)
    }

    @Test
    fun lista_con_puestos_queda_lista_como_contenido() {
        val sesion = FakeSesionRepositorio().apply {
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.Ok(listOf(puesto())) }

        val vm = MallaLineaViewModel(sesion, malla)

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Listo<List<PuestoMalla>>)
        assertEquals(1, (estado as EstadoPantalla.Listo<List<PuestoMalla>>).datos.size)
    }

    @Test
    fun lista_vacia_es_vacio_legitimo_no_error() {
        val sesion = FakeSesionRepositorio().apply {
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.Ok(emptyList()) }

        val vm = MallaLineaViewModel(sesion, malla)

        assertTrue(vm.estado.value is EstadoPantalla.VacioLegitimo)
    }

    @Test
    fun sin_alcance_muestra_error_con_causa_y_siguiente_paso() {
        val sesion = FakeSesionRepositorio().apply {
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.SinAlcance }

        val vm = MallaLineaViewModel(sesion, malla)

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Error)
    }

    @Test
    fun sin_linea_resuelta_muestra_error_en_vez_de_reventar() {
        val sesion = FakeSesionRepositorio().apply { quienSoyResultado = null }
        val malla = FakeMallaRepositorio()

        val vm = MallaLineaViewModel(sesion, malla)

        assertTrue(vm.estado.value is EstadoPantalla.Error)
    }

    @Test
    fun sin_conexion_usa_el_texto_literal_del_ejemplo_de_03_3_11() {
        val sesion = FakeSesionRepositorio().apply {
            quienSoyResultado = QuienSoy(1, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val malla = FakeMallaRepositorio().apply { resultado = ResultadoMalla.SinConexion }

        val vm = MallaLineaViewModel(sesion, malla)

        val estado = vm.estado.value as EstadoPantalla.Error
        assertEquals("No se pudo cargar la línea.", estado.causa)
        assertEquals("Reintentando en 5 s.", estado.accionSugerida)
    }
}
