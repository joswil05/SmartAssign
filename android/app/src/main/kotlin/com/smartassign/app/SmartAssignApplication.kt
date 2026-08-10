package com.smartassign.app

import android.app.Application
import dagger.hilt.android.HiltAndroidApp

/** Punto de entrada de Hilt — grafo de dependencias de toda la app (E6.3). */
@HiltAndroidApp
class SmartAssignApplication : Application()
