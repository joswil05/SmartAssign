package com.smartassign.app.ui.malla

import androidx.compose.foundation.layout.Column
import androidx.compose.ui.test.junit4.createComposeRule
import androidx.compose.ui.test.onNodeWithTag
import androidx.compose.ui.test.onNodeWithText
import com.smartassign.app.data.malla.OcupantePuesto
import com.smartassign.app.data.malla.PuestoMalla
import org.junit.Rule
import org.junit.Test

/**
 * UT-E14.5 (docs/PROGRESO.md): "escala de grises" — 03 §5.3, literal:
 * *"la app debe ser completamente operable en escala de grises. Es la
 * prueba de aceptación de este principio."* No convierte píxeles a
 * gris: prueba la ARQUITECTURA de tres canales que §5.3 exige (Color /
 * Forma / Texto) sin que ninguna aserción dependa del color — si toda la
 * información sigue siendo distinguible leyendo solo texto y
 * presencia/ausencia de forma, es, por definición, operable sin color.
 *
 * `TarjetaDePuesto` es "el componente central de la app" (03 §3.1) y el
 * que más literalmente ilustra el principio — la propia tabla de §5.3 usa
 * "relevo crítico" (la barra de fatiga) como su ejemplo.
 *
 * La barra de fatiga ya tenía color y forma (icono + grosor), pero le
 * faltaba el canal de texto: las dos filas de fatiga de 03 §7.1 nunca
 * habían llegado a `MicroCopiaDePuesto` (backend, `LineaEndpoints.cs`,
 * misma UT). Esta prueba, del lado Android, confirma que `TarjetaDePuesto`
 * ya renderizaba ese texto sin cambios propios — solo le faltaba que el
 * servidor lo mandara.
 */
class TarjetaDePuestoOperableEnGrisesTest {

    @get:Rule
    val compose = createComposeRule()

    private fun ocupante(nombre: String = "Persona de prueba") =
        OcupantePuesto(personalId = 1, nombreCompleto = nombre, ficha = "F0001", categoria = "operario", dobleTurno = false)

    private fun puestoConFatiga(id: Int, nivel: String?, microCopia: String) = PuestoMalla(
        id = id, codigo = "L4-R0$id", nombrePuesto = "Puesto $id", tipo = "rotativo", situacion = "ocupado",
        ocupante = ocupante(), indicadorMedico = 0, microCopia = microCopia,
        nivelFatiga = nivel, excesoFatiga = if (nivel == null) null else 50.0
    )

    // ═══ Canal de texto: cada situación tiene su propio mensaje, nunca solo color ═══

    @Test
    fun cada_situacion_de_puesto_se_distingue_por_texto_propio_sin_depender_del_color() {
        val situacionesConTexto = mapOf(
            "libre" to "Esperando el arranque del turno",
            "ocupado" to "Asignado automáticamente por asistencia",
            "vacante_critica" to "Sin titular ni suplente disponible",
            "descubierto" to "Sin ocupante — pendiente de cubrir",
            "fuera_de_operacion" to "Puesto no requerido por el SKU de hoy",
        )

        // Un solo setContent (la regla de Compose no admite más de uno
        // por prueba) con las cinco tarjetas a la vez — verifica de paso
        // que ninguna pisa el texto de otra.
        compose.setContent {
            Column {
                situacionesConTexto.entries.forEachIndexed { indice, (situacion, texto) ->
                    TarjetaDePuesto(PuestoMalla(
                        id = indice, codigo = "L4-A0$indice", nombrePuesto = "Puesto $indice", tipo = "fijo",
                        situacion = situacion, ocupante = null, indicadorMedico = 0, microCopia = texto,
                    ))
                }
            }
        }

        // 03 §5.3: el texto por sí solo debe bastar para entender el
        // estado — sin leer `situacion` ni ningún color.
        situacionesConTexto.forEach { (situacion, texto) ->
            compose.onNodeWithText(texto).assertExists(
                "la situación \"$situacion\" debe traer su propio texto distintivo (03 §7.1), no depender del color de la franja"
            )
        }

        // Los cinco mensajes son literalmente distintos entre sí — si dos
        // coincidieran, el color sería el único desempate real.
        org.junit.Assert.assertEquals(situacionesConTexto.size, situacionesConTexto.values.toSet().size)
    }

    // ═══ Canal de forma: fatiga sugerida/crítica trae icono, normal no — nunca solo color ═══

    @Test
    fun la_fatiga_sugerida_trae_icono_propio_ademas_del_color() {
        val puesto = puestoConFatiga(1, "sugerido", "Relevo sugerido — 62 minutos en el puesto")
        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-1-barra-fatiga-icono").assertExists()
        compose.onNodeWithText("Relevo sugerido — 62 minutos en el puesto").assertExists()
    }

    @Test
    fun la_fatiga_critica_trae_un_icono_distinto_ademas_del_color() {
        val puesto = puestoConFatiga(2, "critico", "Límite ergonómico superado — 95 minutos en el puesto")
        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-2-barra-fatiga-icono").assertExists()
        compose.onNodeWithText("Límite ergonómico superado — 95 minutos en el puesto").assertExists()
    }

    @Test
    fun la_fatiga_normal_no_trae_icono_la_ausencia_de_forma_tambien_es_informacion() {
        // "normal" no está en el catálogo de fatiga de §7.1 (solo sugerida
        // y crítica lo están) — la micro-copia sigue siendo la de
        // situación, y la ausencia del icono (frente a sugerido/crítico)
        // es en sí misma la señal de forma que distingue este nivel.
        val puesto = puestoConFatiga(3, "normal", "Asignado automáticamente por asistencia")
        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-3-barra-fatiga-icono").assertDoesNotExist()
        compose.onNodeWithTag("tarjeta-puesto-3-barra-fatiga").assertExists()
    }

    @Test
    fun sin_dato_real_de_fatiga_no_se_dibuja_ninguna_barra_ni_inventada_ni_vacia() {
        // §1.3: nunca se dibuja una barra sin dato real detrás — un fijo,
        // o un rotativo sin ocupante, no trae nivelFatiga.
        val puesto = PuestoMalla(
            id = 4, codigo = "L4-A04", nombrePuesto = "Puesto 4", tipo = "fijo", situacion = "libre",
            ocupante = null, indicadorMedico = 0, microCopia = "Esperando el arranque del turno",
            nivelFatiga = null, excesoFatiga = null,
        )
        compose.setContent { TarjetaDePuesto(puesto) }

        compose.onNodeWithTag("tarjeta-puesto-4-barra-fatiga").assertDoesNotExist()
    }
}
