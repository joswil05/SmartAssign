package com.smartassign.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.smartassign.app.ui.altadispositivo.AltaDispositivoScreen
import com.smartassign.app.ui.arranque.ArranqueScreen
import com.smartassign.app.ui.destinos.PanelBolsonScreen
import com.smartassign.app.ui.destinos.PanelPlantaScreen
import com.smartassign.app.ui.destinos.SinLineaScreen
import com.smartassign.app.ui.frescura.BannerSinSincronizar
import com.smartassign.app.ui.frescura.BannerSinSincronizarViewModel
import com.smartassign.app.ui.login.LoginScreen
import com.smartassign.app.ui.malla.MallaLineaScreen
import com.smartassign.app.ui.navegacion.Rutas
import com.smartassign.app.ui.paro.CronometroDeParo
import com.smartassign.app.ui.paro.CronometroDeParoViewModel
import com.smartassign.app.ui.pin.PinScreen
import com.smartassign.app.ui.theme.SmartAssignTheme
import dagger.hilt.android.AndroidEntryPoint

/**
 * Punto de entrada. El mapa de navegación completo de 02 §1.0/§1.1 vive
 * aquí desde la etapa E6.3: alta de dispositivo → login/PIN → el destino
 * que decide el rol y la línea vigente, nunca elegido de una lista (§2.3).
 */
@AndroidEntryPoint
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            SmartAssignTheme {
                GrafoDeNavegacion()
            }
        }
    }
}

/**
 * E11.3 (§11.1, 03 §3.8): el cronómetro de paro vive AQUÍ, envolviendo el
 * `NavHost` en vez de vivir dentro de un `composable(...)` — es la única
 * forma de que sea "visible en todo momento, aunque el supervisor navegue
 * a otras partes de la aplicación". `hiltViewModel()` sin argumentos, en
 * este punto del árbol, resuelve contra el propio `ComponentActivity`
 * (no contra un destino del grafo), así que sobrevive intacto a cualquier
 * navegación — se reinicia solo si la Activity misma se recrea.
 *
 * UT-E13.4 añade [BannerSinSincronizar] con el mismo criterio, colgado
 * **por encima** de [CronometroDeParo] — 03 §3.8, literal: la barra del
 * cronómetro va *"por debajo del banner de conexión si ambos están
 * activos"*. Es el banner que el propio docstring de E11.3 dejó anotado
 * como pendiente.
 */
@Composable
fun GrafoDeNavegacion(
    controlador: NavHostController = rememberNavController(),
    cronometroViewModel: CronometroDeParoViewModel = hiltViewModel(),
    bannerViewModel: BannerSinSincronizarViewModel = hiltViewModel()
) {
    val paroActivo by cronometroViewModel.paro.collectAsState()
    val mostrarBanner by bannerViewModel.mostrarBanner.collectAsState()

    Column(modifier = Modifier.fillMaxSize()) {
        BannerSinSincronizar(visible = mostrarBanner)
        CronometroDeParo(paro = paroActivo)

        NavHost(
            navController = controlador,
            startDestination = Rutas.ARRANQUE,
            modifier = Modifier.weight(1f)
        ) {
            composable(Rutas.ARRANQUE) {
                ArranqueScreen(onNavegarA = { ruta -> navegarYReemplazarArranque(controlador, ruta) })
            }
            composable(Rutas.ALTA_DISPOSITIVO) {
                AltaDispositivoScreen(onServidorConfigurado = {
                    controlador.navigate(Rutas.LOGIN) { popUpTo(Rutas.ALTA_DISPOSITIVO) { inclusive = true } }
                })
            }
            composable(Rutas.LOGIN) {
                LoginScreen(onAutenticado = { destino -> navegarYLimpiarHistorialDeSesion(controlador, destino) })
            }
            composable(Rutas.PIN) {
                PinScreen(
                    onAutenticado = { destino -> navegarYLimpiarHistorialDeSesion(controlador, destino) },
                    onVolverALogin = {
                        controlador.navigate(Rutas.LOGIN) { popUpTo(Rutas.ARRANQUE) { inclusive = false } }
                    }
                )
            }
            composable(Rutas.PANEL_PLANTA) { PanelPlantaScreen() }
            composable(Rutas.MALLA_LINEA) { MallaLineaScreen() }
            composable(Rutas.PANEL_BOLSON) { PanelBolsonScreen() }
            composable(Rutas.SIN_LINEA) { SinLineaScreen() }
        }
    }
}

/** El splash nunca queda en el historial — al decidir su destino, se reemplaza. */
private fun navegarYReemplazarArranque(controlador: NavHostController, ruta: String) {
    controlador.navigate(ruta) { popUpTo(Rutas.ARRANQUE) { inclusive = true } }
}

/** Tras autenticar, ni login ni PIN quedan en el historial — "atrás" no debe volver a pedir credenciales. */
private fun navegarYLimpiarHistorialDeSesion(controlador: NavHostController, destino: String) {
    controlador.navigate(destino) { popUpTo(Rutas.ARRANQUE) { inclusive = true } }
}
