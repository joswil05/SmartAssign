package com.smartassign.app.ui.arranque

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.sesion.ResultadoAuth
import com.smartassign.app.data.sesion.SesionRepositorio
import com.smartassign.app.ui.estado.EstadoPantalla
import com.smartassign.app.ui.navegacion.Rutas
import com.smartassign.app.ui.navegacion.destinoTrasAutenticar
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * El árbol de decisión de 02 §1.1 (splash + verificación), expresado como
 * [EstadoPantalla]<ruta>: mientras decide es `Cargando`, si algo impide
 * decidir (sin red hacia un servidor ya conocido) es `Error` — con causa y
 * siguiente paso, nunca en blanco (§12.4) — y en cuanto sabe a dónde ir,
 * `Listo(ruta)` dispara la navegación. No hay `VacioLegitimo` ni
 * `FueraDeOperacion` aquí: esta pantalla nunca los necesita, y no pasa
 * nada por declarar el tipo completo — nunca se construyen.
 */
@HiltViewModel
class ArranqueViewModel @Inject constructor(
    private val repositorio: SesionRepositorio
) : ViewModel() {

    private val _estado = MutableStateFlow<EstadoPantalla<String>>(EstadoPantalla.Cargando)
    val estado: StateFlow<EstadoPantalla<String>> = _estado.asStateFlow()

    init {
        decidir()
    }

    fun decidir() {
        _estado.value = EstadoPantalla.Cargando
        viewModelScope.launch {
            if (!repositorio.servidorConfigurado()) {
                _estado.value = EstadoPantalla.Listo(Rutas.ALTA_DISPOSITIVO)
                return@launch
            }

            if (!repositorio.haySesionGuardada()) {
                _estado.value = EstadoPantalla.Listo(Rutas.LOGIN)
                return@launch
            }

            if (sesionBloqueadaPorInactividad()) {
                _estado.value = EstadoPantalla.Listo(Rutas.PIN)
                return@launch
            }

            when (val resultado = repositorio.renovarSesion()) {
                is ResultadoAuth.Ok -> {
                    val quienSoy = repositorio.quienSoy()
                    _estado.value = if (quienSoy != null) {
                        EstadoPantalla.Listo(destinoTrasAutenticar(quienSoy.rol, quienSoy.linea))
                    } else {
                        EstadoPantalla.Error(
                            "No se pudo confirmar tu sesión con el servidor.",
                            "Reintentando en unos segundos."
                        )
                    }
                }

                is ResultadoAuth.Rechazo -> _estado.value = EstadoPantalla.Listo(Rutas.LOGIN)

                ResultadoAuth.SinConexion -> _estado.value = EstadoPantalla.Error(
                    "No se pudo llegar al servidor.",
                    "Revisa que estés en la red de planta y vuelve a intentar."
                )
            }
        }
    }

    /**
     * 02 §1.1: <¿Sesión bloqueada por inactividad?> — el umbral es
     * `inactividad_bloqueo_sesion_min` (04 §9), declarado "a definir" y
     * sin fila sembrada, igual que `ventana_arranque_min` en E4.5/E5.7.
     * Inventar un número aquí violaría R2. Mientras el cliente no lo fije
     * y no exista un endpoint que lo entregue, la regla nunca se activa
     * por sí sola — el nodo queda estructuralmente presente y listo para
     * conectarse el día que el parámetro exista.
     */
    private fun sesionBloqueadaPorInactividad(): Boolean = false
}
