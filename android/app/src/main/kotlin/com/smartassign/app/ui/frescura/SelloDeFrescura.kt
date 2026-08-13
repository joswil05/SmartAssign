package com.smartassign.app.ui.frescura

import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.produceState
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import com.smartassign.app.ui.theme.ColorAlerta
import com.smartassign.app.ui.theme.TextSecondary
import com.smartassign.app.ui.theme.TypeCaption
import java.time.Duration
import java.time.Instant
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive

/**
 * UT-E13.4 (docs/PROGRESO.md): "Sello de frescura + degradación visual"
 * (00 §D4, 03 §3.7).
 *
 * `antiguedadMaximaMin` — 5 min, el mismo criterio que
 * `duracion_maxima_transito` (E8.6): **04 §9 da un número real** para
 * `antiguedad_maxima_datos_min` — *"valor inicial provisional: 5 min"*
 * — no es un "a definir" al que haya que negarse a inventar (R2 no
 * bloquea esto, el propio documento ya lo fijó). Como el cliente Android
 * todavía no tiene ningún endpoint que exponga `Parametro` (ninguna UT
 * lo ha pedido), se declara aquí como constante — mismo patrón que el
 * `ISNULL(..., 15)` de `sp_CaducarTransitos` en el servidor, trasladado
 * al cliente porque aquí no hay servidor al que preguntarle en cada
 * pantalla.
 */
const val ANTIGUEDAD_MAXIMA_DATOS_MIN = 5L

enum class NivelFrescura { Discreto, Alerta }

/** Nunca negativo — un reloj local adelantado respecto al servidor no debe mostrar un dato "del futuro". */
fun minutosTranscurridos(cacheadoEn: Instant, ahora: Instant): Long =
    Duration.between(cacheadoEn, ahora).toMinutes().coerceAtLeast(0)

fun nivelFrescura(
    cacheadoEn: Instant,
    ahora: Instant,
    antiguedadMaximaMin: Long = ANTIGUEDAD_MAXIMA_DATOS_MIN
): NivelFrescura =
    if (minutosTranscurridos(cacheadoEn, ahora) > antiguedadMaximaMin) NivelFrescura.Alerta else NivelFrescura.Discreto

/** 03 §3.7, literal: "Datos de hace 2 min". */
fun textoSelloDeFrescura(cacheadoEn: Instant, ahora: Instant): String =
    "Datos de hace ${minutosTranscurridos(cacheadoEn, ahora)} min"

/**
 * "Línea discreta bajo la cabecera" (03 §3.7). Recalcula sola cada
 * minuto — mismo patrón `produceState`+`delay` que `CronometroDeParo`
 * (E11.3) — para que un dato que envejece más allá del umbral pase a
 * `Alerta` sin que la pantalla necesite recomponerse por otra razón.
 */
@Composable
fun SelloDeFrescura(cacheadoEn: Instant, modifier: Modifier = Modifier) {
    val ahora by produceState(initialValue = Instant.now(), key1 = cacheadoEn) {
        while (isActive) {
            value = Instant.now()
            delay(15_000L) // el sello se mide en minutos — 15 s de margen sobra, no hace falta el tick de 1 s del cronómetro
        }
    }
    val nivel = nivelFrescura(cacheadoEn, ahora)

    Text(
        text = textoSelloDeFrescura(cacheadoEn, ahora),
        style = TypeCaption,
        color = if (nivel == NivelFrescura.Alerta) ColorAlerta else TextSecondary,
        modifier = modifier.testTag("sello-de-frescura")
    )
}
