package com.smartassign.app.ui.arranque

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.red.LineaVigenteResponse
import com.smartassign.app.data.sesion.FakeSesionRepositorio
import com.smartassign.app.data.sesion.QuienSoy
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.ui.estado.EstadoPantalla
import com.smartassign.app.ui.navegacion.Rutas
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/** El árbol de decisión completo de 02 §1.1, rama por rama. */
class ArranqueViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun sin_servidor_configurado_va_a_alta_de_dispositivo() {
        val repo = FakeSesionRepositorio().apply { configurado = false }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Listo<String>)
        assertEquals(Rutas.ALTA_DISPOSITIVO, (estado as EstadoPantalla.Listo<String>).datos)
    }

    @Test
    fun servidor_configurado_pero_sin_sesion_guardada_va_a_login() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = false
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.LOGIN, estado.datos)
    }

    @Test
    fun sesion_guardada_pero_el_refresh_es_rechazado_vuelve_a_login() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.Rechazo("REFRESH_EXPIRADO")
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.LOGIN, estado.datos)
    }

    @Test
    fun sesion_valida_de_coordinador_resuelve_al_panel_de_planta() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.Ok(1, "coordinador", "Coord")
            quienSoyResultado = QuienSoy(1, "coordinador", "Coord", null)
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.PANEL_PLANTA, estado.datos)
    }

    @Test
    fun sesion_valida_de_supervisor_de_L8_resuelve_al_panel_bolson() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.Ok(2, "supervisor", "Sup")
            quienSoyResultado = QuienSoy(2, "supervisor", "Sup", LineaVigenteResponse(8, "L8", "Bolsón", esBolson = true))
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.PANEL_BOLSON, estado.datos)
    }

    @Test
    fun sesion_valida_de_supervisor_de_linea_normal_resuelve_a_la_malla() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.Ok(3, "supervisor", "Sup")
            quienSoyResultado = QuienSoy(3, "supervisor", "Sup", LineaVigenteResponse(4, "L4", "Línea 4", esBolson = false))
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.MALLA_LINEA, estado.datos)
    }

    @Test
    fun sesion_valida_de_supervisor_sin_linea_resuelve_al_terminal_sin_linea() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.Ok(4, "supervisor", "Sup")
            quienSoyResultado = QuienSoy(4, "supervisor", "Sup", null)
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value as EstadoPantalla.Listo<String>
        assertEquals(Rutas.SIN_LINEA, estado.datos)
    }

    @Test
    fun sin_conexion_con_sesion_guardada_muestra_error_con_causa_y_siguiente_paso() {
        val repo = FakeSesionRepositorio().apply {
            configurado = true
            sesionGuardada = true
            resultadoRenovar = ResultadoAuth.SinConexion
        }
        val vm = ArranqueViewModel(repo)

        val estado = vm.estado.value
        assertTrue(estado is EstadoPantalla.Error)
        val error = estado as EstadoPantalla.Error
        assertTrue(error.causa.isNotBlank())
        assertTrue(error.accionSugerida.isNotBlank())
    }
}
