package com.smartassign.app.data.conectividad

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject
import javax.inject.Singleton

/**
 * `Conectado` de arranque a propósito (optimista): antes del primer
 * intento de red no hay ninguna evidencia de lo contrario, y §12.1 exige
 * bloquear cuando se SABE que no hay conexión — inventar una duda no es
 * más honesto que asumir lo mejor. El único costo es que la primera
 * escritura tras abrir la app, si de verdad no hay red, se intenta una
 * vez y falla por la vía reactiva de siempre (`IOException` → resultado
 * `SinConexion`) antes de que este estado se ponga al día — nunca deja a
 * nadie creyendo que algo se guardó cuando no fue así.
 */
@Singleton
class ConectividadRepositorioImpl @Inject constructor() : ConectividadRepositorio {
    private val _estado = MutableStateFlow<EstadoConectividad>(EstadoConectividad.Conectado)
    override val estado: StateFlow<EstadoConectividad> = _estado

    override fun reportarAlcanzado() {
        _estado.value = EstadoConectividad.Conectado
    }

    override fun reportarInalcanzable() {
        _estado.value = EstadoConectividad.SinConexion
    }
}
