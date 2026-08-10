package com.smartassign.app.ui.asignacion

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.smartassign.app.data.asignacion.AsignacionRepositorio
import com.smartassign.app.data.asignacion.ResultadoAsignar
import com.smartassign.app.data.asignacion.ResultadoSugerencia
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.personal.PersonalRepositorio
import com.smartassign.app.data.personal.ResultadoPersonal
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch
import java.util.UUID
import javax.inject.Inject

/**
 * Conecta lo que E6.5/E6.6/E6.7/E6.8 ya construyeron y verificaron por
 * separado, para que un supervisor pueda de verdad llenar su línea desde
 * un teléfono real (→ PC-3, 07 §6). Ningún paso reimplementa una regla:
 * la ficha se resuelve contra `GET /api/personal/por-ficha` (E6.6), el
 * destino contra `sp_SugerirPuesto` (E6.7) y la escritura contra
 * `sp_AsignarPersona` (E6.8) — este ViewModel solo encadena las tres
 * llamadas y traduce sus resultados reales, nunca inventa un código o un
 * mensaje que el servidor no haya devuelto (R2).
 */
sealed interface EstadoFlujoAsignacion {
    data object Cargando : EstadoFlujoAsignacion

    data class ListoParaConfirmar(
        val ficha: String,
        val personalId: Int,
        val puestoId: Int,
        val destinoPuesto: String,
        val destinoTipo: String,
        val idempotencyKey: String
    ) : EstadoFlujoAsignacion

    data object Confirmando : EstadoFlujoAsignacion
    data class Confirmado(val asignacionId: Long) : EstadoFlujoAsignacion

    /** Causa concreta + acción concreta (§12.4) — nunca un código crudo, tanto si viene de la red como del servidor. */
    data class Error(val causa: String, val accionSugerida: String) : EstadoFlujoAsignacion

    /** El 409 nominal de B1 ("Fulano acaba de ser registrado..."): mensaje real del servidor, no reescrito aquí. */
    data class RechazadoAlConfirmar(val mensaje: String) : EstadoFlujoAsignacion
}

@HiltViewModel
class FlujoAsignacionViewModel @Inject constructor(
    private val personalRepositorio: PersonalRepositorio,
    private val asignacionRepositorio: AsignacionRepositorio
) : ViewModel() {

    private val _estado = MutableStateFlow<EstadoFlujoAsignacion>(EstadoFlujoAsignacion.Cargando)
    val estado: StateFlow<EstadoFlujoAsignacion> = _estado.asStateFlow()

    /**
     * `puestosDeLinea`: la malla que el supervisor ya tiene cargada en
     * pantalla — se usa solo para mostrar el código/tipo del puesto
     * sugerido (03 §3.3), nunca para decidir a cuál asignar; esa decisión
     * es enteramente del servidor (`sp_SugerirPuesto`).
     */
    fun iniciar(ficha: String, puestosDeLinea: List<PuestoMalla>) {
        _estado.value = EstadoFlujoAsignacion.Cargando
        viewModelScope.launch {
            when (val persona = personalRepositorio.porFicha(ficha)) {
                is ResultadoPersonal.Ok -> resolverSugerencia(ficha, persona.persona.personalId, puestosDeLinea)

                ResultadoPersonal.NoEncontrado -> _estado.value = EstadoFlujoAsignacion.Error(
                    causa = "No hay nadie registrado con la ficha $ficha.",
                    accionSugerida = "Revisa el gafete o vuelve a escanearlo."
                )

                ResultadoPersonal.SinConexion -> _estado.value = EstadoFlujoAsignacion.Error(
                    causa = "No se pudo consultar el padrón de personal.",
                    accionSugerida = "Revisa tu conexión e inténtalo de nuevo."
                )
            }
        }
    }

    private suspend fun resolverSugerencia(ficha: String, personalId: Int, puestosDeLinea: List<PuestoMalla>) {
        when (val sugerencia = asignacionRepositorio.sugerirPuesto(personalId)) {
            is ResultadoSugerencia.Ok -> {
                val puesto = puestosDeLinea.firstOrNull { it.id == sugerencia.puestoId }
                _estado.value = EstadoFlujoAsignacion.ListoParaConfirmar(
                    ficha = ficha,
                    personalId = personalId,
                    puestoId = sugerencia.puestoId,
                    destinoPuesto = puesto?.codigo ?: "Puesto ${sugerencia.puestoId}",
                    destinoTipo = if (puesto?.tipo == "fijo") "Fijo" else "Rotativo",
                    idempotencyKey = UUID.randomUUID().toString()
                )
            }

            is ResultadoSugerencia.SinSugerencia -> _estado.value = EstadoFlujoAsignacion.Error(
                causa = sugerencia.mensaje.ifBlank { "No hay un puesto disponible para asignar." },
                accionSugerida = "Consulta con el Coordinador."
            )

            ResultadoSugerencia.SinConexion -> _estado.value = EstadoFlujoAsignacion.Error(
                causa = "No se pudo calcular un puesto para esta persona.",
                accionSugerida = "Revisa tu conexión e inténtalo de nuevo."
            )
        }
    }

    /**
     * Solo actúa desde `ListoParaConfirmar` — si el estado ya cambió (p.
     * ej. una confirmación en curso), un segundo toque no reenvía nada.
     */
    fun confirmar() {
        val actual = _estado.value as? EstadoFlujoAsignacion.ListoParaConfirmar ?: return
        _estado.value = EstadoFlujoAsignacion.Confirmando
        viewModelScope.launch {
            when (val resultado = asignacionRepositorio.asignarPersona(
                puestoId = actual.puestoId,
                personalId = actual.personalId,
                idempotencyKey = actual.idempotencyKey
            )) {
                is ResultadoAsignar.Ok -> _estado.value = EstadoFlujoAsignacion.Confirmado(resultado.asignacionId)

                is ResultadoAsignar.Rechazado -> _estado.value = EstadoFlujoAsignacion.RechazadoAlConfirmar(
                    resultado.mensaje.ifBlank { "No se pudo completar la asignación." }
                )

                ResultadoAsignar.SinConexion -> _estado.value = EstadoFlujoAsignacion.RechazadoAlConfirmar(
                    "No se pudo completar la asignación. Revisa tu conexión e inténtalo de nuevo."
                )
            }
        }
    }
}
