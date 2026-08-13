package com.smartassign.app

import android.app.Application
import com.smartassign.app.data.conectividad.ConectividadTicker
import dagger.hilt.android.HiltAndroidApp
import javax.inject.Inject

/** Punto de entrada de Hilt — grafo de dependencias de toda la app (E6.3). */
@HiltAndroidApp
class SmartAssignApplication : Application() {

    // Inyección de campo, no de constructor — Application no pasa por un
    // constructor de Hilt; el componente termina de inicializarse justo
    // antes de que este onCreate() corra.
    @Inject
    lateinit var conectividadTicker: ConectividadTicker

    override fun onCreate() {
        super.onCreate()
        // UT-E13.5 (05 §4.3): el latido corre para toda la vida del
        // proceso, no atado a ninguna pantalla — es la mitad que E13.3
        // dejó pendiente explícitamente.
        conectividadTicker.iniciar()
    }
}
