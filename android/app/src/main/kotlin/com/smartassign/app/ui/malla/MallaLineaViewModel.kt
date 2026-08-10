package com.smartassign.app.ui.malla

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.malla.MallaRepositorio
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.malla.ResultadoMalla
import com.smartassign.app.data.sesion.SesionRepositorio
import com.smartassign.app.ui.estado.EstadoPantalla
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

private const val ESPERA_REINTENTO_MS = 5_000L

/**
 * §2.3: la línea nunca se elige de una lista — se resuelve de nuevo en
 * cada entrada a la pantalla contra `/api/auth/me` (E6.3), nunca desde
 * un argumento de navegación que pudiera quedar obsoleto.
 */
@HiltViewModel
class MallaLineaViewModel @Inject constructor(
    private val sesionRepositorio: SesionRepositorio,
    private val mallaRepositorio: MallaRepositorio
) : ViewModel() {

    private val _estado = MutableStateFlow<EstadoPantalla<List<PuestoMalla>>>(EstadoPantalla.Cargando)
    val estado: StateFlow<EstadoPantalla<List<PuestoMalla>>> = _estado.asStateFlow()

    init {
        cargar()
    }

    fun cargar() {
        _estado.value = EstadoPantalla.Cargando
        viewModelScope.launch {
            val quienSoy = sesionRepositorio.quienSoy()
            val lineaId = quienSoy?.linea?.id

            if (lineaId == null) {
                _estado.value = EstadoPantalla.Error(
                    "No se pudo confirmar tu línea.",
                    "Reintentando en unos segundos."
                )
                delay(ESPERA_REINTENTO_MS)
                cargar()
                return@launch
            }

            when (val resultado = mallaRepositorio.puestosDeLinea(lineaId)) {
                is ResultadoMalla.Ok -> _estado.value = if (resultado.puestos.isEmpty()) {
                    EstadoPantalla.VacioLegitimo(
                        mensaje = "No hay puestos activos en esta línea.",
                        siguientePaso = "Consulta con el Coordinador si esto no es correcto."
                    )
                } else {
                    EstadoPantalla.Listo(resultado.puestos)
                }

                ResultadoMalla.SinAlcance -> _estado.value = EstadoPantalla.Error(
                    "No tienes acceso a esta línea.",
                    "Vuelve a iniciar sesión o consulta con el Coordinador."
                )

                ResultadoMalla.SinConexion -> {
                    // Texto literal del ejemplo de 03 §3.11 — y de verdad
                    // reintenta a los 5 s, no es solo la frase.
                    _estado.value = EstadoPantalla.Error(
                        "No se pudo cargar la línea.",
                        "Reintentando en 5 s."
                    )
                    delay(ESPERA_REINTENTO_MS)
                    cargar()
                }
            }
        }
    }
}
