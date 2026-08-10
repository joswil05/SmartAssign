package com.smartassign.app.ui.confirmacion

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.personal.PersonalRepositorio
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.personal.ResultadoPersonal
import com.smartassign.app.ui.estado.EstadoPantalla
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

/**
 * 03 §3.3 / 00 §E1 §12.2: resuelve la ficha decodificada del gafete
 * (E6.5) contra el padrón real, para que el modal pueda mostrar nombre,
 * ficha, categoría y restricciones médicas explícitas **antes** de que el
 * supervisor confirme nada. No consolida ninguna asignación — ese
 * endpoint todavía no existe (llega con E6.7/E6.8); `onConfirmar` es un
 * callback puro hacia quien invoque este modal.
 */
@HiltViewModel
class ConfirmacionIdentidadViewModel @Inject constructor(
    private val repositorio: PersonalRepositorio
) : ViewModel() {

    private val _estado = MutableStateFlow<EstadoPantalla<PersonaConfirmacion>>(EstadoPantalla.Cargando)
    val estado: StateFlow<EstadoPantalla<PersonaConfirmacion>> = _estado.asStateFlow()

    fun cargar(ficha: String) {
        _estado.value = EstadoPantalla.Cargando
        viewModelScope.launch {
            _estado.value = when (val resultado = repositorio.porFicha(ficha)) {
                is ResultadoPersonal.Ok -> EstadoPantalla.Listo(resultado.persona)
                // 03 §12.4: causa concreta + acción concreta, nunca un código de error.
                is ResultadoPersonal.NoEncontrado -> EstadoPantalla.Error(
                    causa = "No hay nadie registrado con la ficha $ficha.",
                    accionSugerida = "Revisa el gafete o vuelve a escanearlo."
                )
                is ResultadoPersonal.SinConexion -> EstadoPantalla.Error(
                    causa = "No se pudo consultar el padrón de personal.",
                    accionSugerida = "Revisa tu conexión e inténtalo de nuevo."
                )
            }
        }
    }
}
