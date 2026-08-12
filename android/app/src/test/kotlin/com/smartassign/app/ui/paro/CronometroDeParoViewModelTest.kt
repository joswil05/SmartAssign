package com.smartassign.app.ui.paro

import com.smartassign.app.MainDispatcherRule
import java.time.Instant
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Rule
import org.junit.Test

/**
 * §11.1: arranca al confirmar el paro, se detiene solo al reanudar
 * producción explícitamente — nada de temporizador automático ni de
 * cierre implícito por inactividad (no está en el LEE de E11.3).
 */
class CronometroDeParoViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun al_crearse_no_hay_ningun_paro_activo() {
        val vm = CronometroDeParoViewModel()

        assertNull(vm.paro.value)
    }

    @Test
    fun paroIniciado_deja_la_categoria_y_el_inicio_exactos() {
        val vm = CronometroDeParoViewModel()
        val inicio = Instant.parse("2026-08-12T10:00:00Z")

        vm.paroIniciado("Mecánico", inicio)

        val paro = vm.paro.value
        assertEquals("Mecánico", paro?.categoria)
        assertEquals(inicio, paro?.inicio)
    }

    @Test
    fun paroReanudado_limpia_el_estado_y_el_cronometro_desaparece() {
        val vm = CronometroDeParoViewModel()
        vm.paroIniciado("Eléctrico", Instant.now())

        vm.paroReanudado()

        assertNull(vm.paro.value)
    }

    @Test
    fun un_segundo_paro_reemplaza_al_primero_sin_arrastrar_su_inicio() {
        val vm = CronometroDeParoViewModel()
        vm.paroIniciado("Mecánico", Instant.parse("2026-08-12T10:00:00Z"))

        val segundoInicio = Instant.parse("2026-08-12T12:00:00Z")
        vm.paroIniciado("Calidad", segundoInicio)

        val paro = vm.paro.value
        assertEquals("Calidad", paro?.categoria)
        assertEquals(segundoInicio, paro?.inicio)
    }
}
