package com.smartassign.app.ui.altadispositivo

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.sesion.SesionRepositorio
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/** 02 §1.0: solo la primera vez, cero tecleo — el QR trae la URL del servidor de planta. */
sealed interface EstadoAltaDispositivo {
    data object Instrucciones : EstadoAltaDispositivo
    data object Escaneando : EstadoAltaDispositivo
    data object Verificando : EstadoAltaDispositivo
    data class NoResponde(val url: String) : EstadoAltaDispositivo
}

@HiltViewModel
class AltaDispositivoViewModel @Inject constructor(
    private val repositorio: SesionRepositorio
) : ViewModel() {

    private val _estado = MutableStateFlow<EstadoAltaDispositivo>(EstadoAltaDispositivo.Instrucciones)
    val estado: StateFlow<EstadoAltaDispositivo> = _estado.asStateFlow()

    fun abrirCamara() {
        _estado.value = EstadoAltaDispositivo.Escaneando
    }

    fun cancelarEscaneo() {
        _estado.value = EstadoAltaDispositivo.Instrucciones
    }

    /** El QR trae directamente la URL del servidor — texto plano, sin protocolo propio (F3). */
    fun alEscanear(urlEscaneada: String, alGuardar: () -> Unit) {
        _estado.value = EstadoAltaDispositivo.Verificando
        viewModelScope.launch {
            if (repositorio.verificarServidor(urlEscaneada)) {
                repositorio.guardarServidor(urlEscaneada)
                alGuardar()
            } else {
                _estado.value = EstadoAltaDispositivo.NoResponde(urlEscaneada)
            }
        }
    }

    fun reintentar() {
        _estado.value = EstadoAltaDispositivo.Instrucciones
    }
}
