package com.smartassign.app.ui.paro

import androidx.lifecycle.ViewModel
import dagger.hilt.android.lifecycle.HiltViewModel
import java.time.Instant
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject

/**
 * Dueño del estado del cronómetro de paro — vive tan arriba como
 * `MainActivity` ([com.smartassign.app.GrafoDeNavegacion], fuera de
 * cualquier destino del `NavHost`), así que sobrevive a la navegación
 * entre pantallas: es justo lo que exige §11.1, "visible en todo momento,
 * aunque el supervisor navegue a otras partes de la aplicación".
 *
 * Quién llama a [paroIniciado]/[paroReanudado] es responsabilidad de la
 * pantalla de registro de paro — que no tiene UT propia todavía en el plan
 * (07_PLAN_DE_EJECUCION.md no declara ninguna). E11.3 solo LEE `§11.1` y
 * `03 §3.8`: construye el cronómetro y su persistencia entre pantallas,
 * no el flujo de "Registrar paro" completo (02 §4.7, fuera de este LEE).
 */
@HiltViewModel
class CronometroDeParoViewModel @Inject constructor() : ViewModel() {

    private val _paro = MutableStateFlow<ParoActivo?>(null)
    val paro: StateFlow<ParoActivo?> = _paro.asStateFlow()

    fun paroIniciado(categoria: String, inicio: Instant = Instant.now()) {
        _paro.value = ParoActivo(categoria, inicio)
    }

    /** §11.1: "Solo se detiene cuando reanuda la producción explícitamente." */
    fun paroReanudado() {
        _paro.value = null
    }
}
