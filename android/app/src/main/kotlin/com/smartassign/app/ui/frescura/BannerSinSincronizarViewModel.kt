package com.smartassign.app.ui.frescura

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.conectividad.ConectividadRepositorio
import com.smartassign.app.data.conectividad.EstadoConectividad
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import javax.inject.Inject

/**
 * Traduce [ConectividadRepositorio] (E13.3) a la única pregunta que
 * [BannerSinSincronizar] necesita responder. Vive tan arriba como
 * `CronometroDeParoViewModel` (fuera del `NavHost`, en
 * `GrafoDeNavegacion`) por la misma razón: un banner "permanente" que
 * desapareciera al navegar entre pantallas no sería permanente.
 */
@HiltViewModel
class BannerSinSincronizarViewModel @Inject constructor(
    conectividad: ConectividadRepositorio
) : ViewModel() {
    val mostrarBanner: StateFlow<Boolean> = conectividad.estado
        .map { it == EstadoConectividad.SinConexion }
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)
}
