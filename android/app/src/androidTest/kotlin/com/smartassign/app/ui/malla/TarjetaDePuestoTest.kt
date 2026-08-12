package com.smartassign.app.ui.malla

import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import com.smartassign.app.data.malla.OcupantePuesto
import com.smartassign.app.data.malla.PuestoMalla
import org.junit.Rule
import org.junit.Test

/** 03 §3.1 — la tarjeta central, con emulador real. */
class TarjetaDePuestoTest {

    @get:Rule
    val compose = createComposeRule()

    @Test
    fun un_puesto_ocupado_muestra_nombre_ficha_categoria_y_microcopia() {
        val puesto = PuestoMalla(
            id = 1, codigo = "L4-A01", nombrePuesto = "Averiero 1", tipo = "fijo",
            situacion = "ocupado",
            ocupante = OcupantePuesto(personalId = 9, nombreCompleto = "María López Hernández", ficha = "4821", categoria = "operador_a"),
            indicadorMedico = 0,
            microCopia = "Asignado automáticamente por asistencia"
        )

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithText("L4-A01").assertExists()
        compose.onNodeWithText("María López Hernández").assertExists()
        compose.onNodeWithText("Ficha 4821 · Operador A").assertExists()
        compose.onNodeWithText("Asignado automáticamente por asistencia").assertExists()
    }

    @Test
    fun el_indicador_medico_solo_aparece_cuando_hay_restricciones_vigentes() {
        val conRestriccion = PuestoMalla(
            id = 2, codigo = "L4-A02", nombrePuesto = "Puesto", tipo = "fijo", situacion = "ocupado",
            ocupante = OcupantePuesto(9, "Juan Pérez", "1000", "operador_a"),
            indicadorMedico = 2, microCopia = "Asignado automáticamente por asistencia"
        )

        compose.setContent { TarjetaDePuesto(conRestriccion) }

        compose.onNodeWithText(" 2", substring = true).assertExists()
    }

    @Test
    fun un_rotativo_descubierto_no_muestra_ocupante_y_dice_pendiente_de_cubrir() {
        val puesto = PuestoMalla(
            id = 3, codigo = "L4-R01", nombrePuesto = "Rotativo", tipo = "rotativo",
            situacion = "descubierto", ocupante = null, indicadorMedico = 0,
            microCopia = "Sin ocupante — pendiente de cubrir"
        )

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-3").assertExists()
        compose.onNodeWithText("Sin ocupante — pendiente de cubrir").assertExists()
    }

    @Test
    fun fuera_de_operacion_muestra_su_microcopia_propia() {
        val puesto = PuestoMalla(
            id = 4, codigo = "L4-A04", nombrePuesto = "Puesto", tipo = "fijo",
            situacion = "fuera_de_operacion", ocupante = null, indicadorMedico = 0,
            microCopia = "Puesto no requerido por el SKU de hoy"
        )

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithText("Puesto no requerido por el SKU de hoy").assertExists()
    }

    // ═══ E7.4 — barra de fatiga y distintivo de doble turno ═══

    private fun puestoRotativoOcupado(nivelFatiga: String?, excesoFatiga: Double?, dobleTurno: Boolean = false) = PuestoMalla(
        id = 5, codigo = "L4-R05", nombrePuesto = "Rotativo", tipo = "rotativo", situacion = "ocupado",
        ocupante = OcupantePuesto(9, "Juan Pérez", "1000", "operario", dobleTurno),
        indicadorMedico = 0, microCopia = "Asignado automáticamente por asistencia",
        nivelFatiga = nivelFatiga, excesoFatiga = excesoFatiga
    )

    @Test
    fun sin_nivel_de_fatiga_no_se_dibuja_ninguna_barra() {
        // §1.3: nunca una barra sin dato real detrás — un fijo, o un
        // rotativo recién arrancado, no traen nivelFatiga.
        val puesto = puestoRotativoOcupado(nivelFatiga = null, excesoFatiga = null)

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-5-barra-fatiga").assertDoesNotExist()
    }

    @Test
    fun nivel_normal_dibuja_la_barra_sin_iconos_de_alerta() {
        val puesto = puestoRotativoOcupado(nivelFatiga = "normal", excesoFatiga = 20.0)

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-5-barra-fatiga").assertExists()
    }

    @Test
    fun nivel_sugerido_dibuja_la_barra_con_su_icono_propio() {
        val puesto = puestoRotativoOcupado(nivelFatiga = "sugerido", excesoFatiga = 120.0)

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-5-barra-fatiga").assertExists()
    }

    @Test
    fun nivel_critico_dibuja_la_barra_con_su_icono_propio() {
        val puesto = puestoRotativoOcupado(nivelFatiga = "critico", excesoFatiga = 180.0)

        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-5-barra-fatiga").assertExists()
    }

    @Test
    fun el_distintivo_de_doble_turno_solo_aparece_cuando_el_ocupante_lo_tiene() {
        // 00 §B7: distintivo permanente, informativo.
        val conDobleTurno = puestoRotativoOcupado(nivelFatiga = null, excesoFatiga = null, dobleTurno = true)

        compose.setContent { TarjetaDePuesto(conDobleTurno) }

        compose.onNodeWithTag("tarjeta-puesto-5-doble-turno").assertExists()
    }

    @Test
    fun sin_doble_turno_no_aparece_el_distintivo() {
        val sinDobleTurno = puestoRotativoOcupado(nivelFatiga = null, excesoFatiga = null, dobleTurno = false)

        compose.setContent { TarjetaDePuesto(sinDobleTurno) }

        compose.onNodeWithTag("tarjeta-puesto-5-doble-turno").assertDoesNotExist()
    }
}
