package com.smartassign.app.ui.pin

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.data.sesion.SesionRepositorio
import com.smartassign.app.ui.navegacion.Rutas
import com.smartassign.app.ui.navegacion.destinoTrasAutenticar
import com.smartassign.app.ui.sesion.MensajesSesion
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

data class PinUiState(
    val pin: String = "",
    val enviando: Boolean = false,
    val error: String? = null
)

/**
 * 02 §1.1: `[Desbloqueo PIN]`, 4–6 dígitos. El conteo de intentos vive en
 * el servidor (04 §6.4) — el cliente no cuenta nada, solo obedece cuando
 * `PIN_MAX_INTENTOS_SESION_CERRADA` llega y fuerza login completo.
 */
@HiltViewModel
class PinViewModel @Inject constructor(
    private val repositorio: SesionRepositorio
) : ViewModel() {

    private val _uiState = MutableStateFlow(PinUiState())
    val uiState: StateFlow<PinUiState> = _uiState.asStateFlow()

    val nombreUsuario: String? get() = repositorio.identidadGuardada()?.nombre

    fun onPinChange(valor: String) {
        if (valor.length <= 6 && valor.all(Char::isDigit)) {
            _uiState.value = _uiState.value.copy(pin = valor, error = null)
        }
    }

    fun verificar(alAutenticar: (String) -> Unit, alVolverALogin: () -> Unit) {
        val estadoActual = _uiState.value
        if (estadoActual.enviando || estadoActual.pin.length < 4) return

        _uiState.value = estadoActual.copy(enviando = true, error = null)
        viewModelScope.launch {
            when (val resultado = repositorio.reentrarConPin(estadoActual.pin)) {
                is ResultadoAuth.Ok -> {
                    val quienSoy = repositorio.quienSoy()
                    _uiState.value = _uiState.value.copy(enviando = false)
                    if (quienSoy != null) {
                        alAutenticar(destinoTrasAutenticar(quienSoy.rol, quienSoy.linea))
                    } else {
                        alAutenticar(Rutas.LOGIN)
                    }
                }

                is ResultadoAuth.Rechazo -> {
                    _uiState.value = _uiState.value.copy(enviando = false, pin = "")
                    if (MensajesSesion.pinFuerzaLogin(resultado.codigo)) {
                        alVolverALogin()
                    } else {
                        _uiState.value = _uiState.value.copy(error = MensajesSesion.paraPin(resultado.codigo))
                    }
                }

                ResultadoAuth.SinConexion -> _uiState.value = _uiState.value.copy(
                    enviando = false,
                    error = MensajesSesion.SIN_CONEXION
                )
            }
        }
    }
}
