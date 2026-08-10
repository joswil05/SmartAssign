package com.smartassign.app

import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.TestDispatcher
import kotlinx.coroutines.test.UnconfinedTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.setMain
import org.junit.rules.TestWatcher
import org.junit.runner.Description

/**
 * `viewModelScope` usa `Dispatchers.Main`, que no existe en un proceso de
 * JVM puro (sin Android) — esta regla lo sustituye por un dispatcher de
 * prueba controlable, igual que cualquier prueba de ViewModel de Compose.
 * `Unconfined`: los `FakeSesionRepositorio` de estas pruebas no suspenden
 * de verdad, así que el estado final queda listo de forma síncrona, sin
 * tener que avanzar el scheduler a mano en cada prueba.
 */
@OptIn(ExperimentalCoroutinesApi::class)
class MainDispatcherRule(private val dispatcher: TestDispatcher = UnconfinedTestDispatcher()) : TestWatcher() {
    override fun starting(description: Description) {
        kotlinx.coroutines.Dispatchers.setMain(dispatcher)
    }

    override fun finished(description: Description) {
        kotlinx.coroutines.Dispatchers.resetMain()
    }
}
