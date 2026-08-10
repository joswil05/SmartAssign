package com.smartassign.app.ui.escaneogafete

import androidx.lifecycle.ViewModel
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject

/**
 * 00 §E1: el gafete codifica el número de ficha, leído con cámara y
 * decodificado **en el dispositivo** — mismo lector compartido que la alta
 * de dispositivo (E6.3, CameraX + ML Kit *bundled*), la imagen nunca sale
 * del teléfono (§12.1). Ningún dato de personal sale hacia terceros
 * (§12.1): este ViewModel no llama a ningún servidor, solo decodifica.
 *
 * Deliberadamente no confirma identidad ni consolida ningún registro —
 * eso es §12.2 (E6.6): "el escaneo por sí solo nunca asienta la
 * asignación". Aquí solo se resuelve el número de ficha impreso en el
 * gafete físico y se entrega a quien llamó.
 */
sealed interface EstadoEscaneoGafete {
    data object Instrucciones : EstadoEscaneoGafete
    data object Escaneando : EstadoEscaneoGafete
}

@HiltViewModel
class EscaneoGafeteViewModel @Inject constructor() : ViewModel() {

    private val _estado = MutableStateFlow<EstadoEscaneoGafete>(EstadoEscaneoGafete.Instrucciones)
    val estado: StateFlow<EstadoEscaneoGafete> = _estado.asStateFlow()

    fun abrirCamara() {
        _estado.value = EstadoEscaneoGafete.Escaneando
    }

    fun cancelarEscaneo() {
        _estado.value = EstadoEscaneoGafete.Instrucciones
    }

    /** El QR del gafete trae solo el número de ficha, texto plano (00 §E1). */
    fun alEscanear(valorCrudo: String, alDecodificar: (String) -> Unit) {
        alDecodificar(valorCrudo.trim())
    }
}
