package com.smartassign.app.ui.comun

import android.provider.Settings
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalInspectionMode

/**
 * UT-E15.6 (08 §P1-8): respeta *Quitar animaciones* de los ajustes de
 * accesibilidad de Android.
 *
 * La app tiene dos animaciones infinitas —el pulso del esqueleto de carga
 * (`PantallaConEstado`) y el pulso de fatiga crítica (`TarjetaDePuesto`)—
 * y ninguna consultaba esta preferencia. Material 3 lo pide expresamente,
 * y quien la activa suele hacerlo por vestibulopatía o por sensibilidad al
 * movimiento: un pulso permanente en pantalla no es un adorno para esa
 * persona.
 *
 * **La regla que no puede romperse al desactivar:** el pulso de fatiga
 * crítica no es decoración, es un canal de información (03 §2.1: *"barra
 * gruesa + icono de alerta + pulso lento"*). Al apagarlo, el elemento debe
 * quedar en **alfa 1** —color pleno, máxima presencia— y nunca congelado a
 * media opacidad, que lo dejaría más débil que el nivel *sugerido* y haría
 * mentir a la escala (§1.3).
 *
 * `ANIMATOR_DURATION_SCALE` es la señal que expone Android para esto: el
 * sistema la pone a 0 tanto desde *Quitar animaciones* (accesibilidad)
 * como desde las opciones de desarrollador. Se lee una sola vez por
 * composición —no es un valor que cambie a mitad de turno— y en modo
 * previsualización se asume activa para que el editor siga mostrando el
 * movimiento.
 */
@Composable
fun animacionesActivas(): Boolean {
    if (LocalInspectionMode.current) return true
    val contexto = LocalContext.current
    return remember(contexto) {
        Settings.Global.getFloat(
            contexto.contentResolver,
            Settings.Global.ANIMATOR_DURATION_SCALE,
            1f
        ) != 0f
    }
}
