package com.smartassign.app.ui.login

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

data class LoginUiState(
    val username: String = "",
    val password: String = "",
    val enviando: Boolean = false,
    val error: String? = null,
    val siguientePaso: String? = null
)

/** 02 §1.1: `[Login usuario]` — usuario+contraseña, un solo mensaje de error sin detalle de qué falló. */
@HiltViewModel
class LoginViewModel @Inject constructor(
    private val repositorio: SesionRepositorio
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState.asStateFlow()

    fun onUsernameChange(valor: String) {
        _uiState.value = _uiState.value.copy(username = valor, error = null)
    }

    fun onPasswordChange(valor: String) {
        _uiState.value = _uiState.value.copy(password = valor, error = null)
    }

    fun iniciarSesion(alAutenticar: (String) -> Unit) {
        val estadoActual = _uiState.value
        if (estadoActual.enviando || estadoActual.username.isBlank() || estadoActual.password.isBlank()) return

        _uiState.value = estadoActual.copy(enviando = true, error = null)
        viewModelScope.launch {
            when (val resultado = repositorio.iniciarSesion(estadoActual.username, estadoActual.password)) {
                is ResultadoAuth.Ok -> {
                    val quienSoy = repositorio.quienSoy()
                    _uiState.value = _uiState.value.copy(enviando = false)
                    if (quienSoy != null) {
                        alAutenticar(destinoTrasAutenticar(quienSoy.rol, quienSoy.linea))
                    } else {
                        alAutenticar(Rutas.LOGIN) // no debería pasar tras un login exitoso; si pasa, se queda en login
                    }
                }

                is ResultadoAuth.Rechazo -> _uiState.value = _uiState.value.copy(
                    enviando = false,
                    error = MensajesSesion.ERROR_LOGIN,
                    siguientePaso = MensajesSesion.ERROR_LOGIN_SIGUIENTE_PASO
                )

                ResultadoAuth.SinConexion -> _uiState.value = _uiState.value.copy(
                    enviando = false,
                    error = MensajesSesion.SIN_CONEXION,
                    siguientePaso = MensajesSesion.SIN_CONEXION_SIGUIENTE_PASO
                )
            }
        }
    }
}
