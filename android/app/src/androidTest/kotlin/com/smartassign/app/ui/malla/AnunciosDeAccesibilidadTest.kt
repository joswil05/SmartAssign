package com.smartassign.app.ui.malla

import androidx.compose.ui.test.assert
import androidx.compose.ui.test.assertContentDescriptionEquals
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import com.smartassign.app.data.malla.OcupantePuesto
import com.smartassign.app.data.malla.PuestoMalla
import org.junit.Rule
import org.junit.Test

/**
 * UT-E15.4 (docs/08_AUDITORIA_FRONTEND.md §P1-1).
 *
 * `TarjetaDePuestoOperableEnGrisesTest` ya cubre el canal visual sin color
 * (texto + forma). Esta prueba cubre el canal que faltaba: **lo que oye
 * quien no ve la pantalla**.
 *
 * El defecto que cierra era concreto y grave: el indicador de restricciones
 * médicas se componía de un icono con `contentDescription = null` y un
 * `Text` con el número suelto, así que TalkBack leía literalmente **"2"**
 * — sin decir dos de qué. Es el dato que `§7.2` protege con una regla dura
 * y que `03 §2.1` señala como *"la única información de la app cuyo
 * malentendido produce daño físico a una persona"*.
 */
class AnunciosDeAccesibilidadTest {

    @get:Rule
    val compose = createComposeRule()

    private fun puestoConMedicas(restricciones: Int) = PuestoMalla(
        id = 7,
        codigo = "L4-R07",
        nombrePuesto = "Puesto 7",
        tipo = "rotativo",
        situacion = "ocupado",
        ocupante = OcupantePuesto(
            personalId = 1,
            nombreCompleto = "Persona de prueba",
            ficha = "F0001",
            categoria = "operario",
            dobleTurno = false
        ),
        indicadorMedico = restricciones,
        microCopia = "Asignado automáticamente por asistencia",
    )

    @Test
    fun el_indicador_medico_se_anuncia_con_su_significado_no_como_un_numero_suelto() {
        compose.setContent { TarjetaDePuesto(puestoConMedicas(2)) }

        compose.onNodeWithTag("tarjeta-puesto-7-indicador-medico")
            .assertContentDescriptionEquals("2 restricciones médicas activas")
    }

    /**
     * Sin restricciones no debe existir el nodo: `03 §3.1` solo pide el
     * indicador cuando hay algo que indicar, y un "0 restricciones médicas
     * activas" hablado sería ruido en cada tarjeta de la malla.
     *
     * (La afirmación explícita de ausencia sí existe, pero en el modal de
     * confirmación —`§3.3`, regla 2—, que es donde la asignación se
     * consolida y donde callar sería peligroso.)
     */
    @Test
    fun sin_restricciones_no_se_anuncia_ningun_indicador_medico() {
        compose.setContent { TarjetaDePuesto(puestoConMedicas(0)) }

        compose.onNodeWithTag("tarjeta-puesto-7-indicador-medico").assertDoesNotExist()
    }

    /**
     * El número sigue estando a la vista: la corrección añade el anuncio
     * hablado, no sustituye el canal visual.
     */
    @Test
    fun el_conteo_sigue_siendo_visible_ademas_de_anunciado() {
        compose.setContent { TarjetaDePuesto(puestoConMedicas(3)) }

        compose.onNodeWithTag("tarjeta-puesto-7-indicador-medico").assertExists()
        compose.onNodeWithText("Asignado automáticamente por asistencia").assertExists()
    }
}
