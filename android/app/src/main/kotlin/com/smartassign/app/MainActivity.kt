package com.smartassign.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.runtime.Composable
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import com.smartassign.app.ui.altadispositivo.AltaDispositivoScreen
import com.smartassign.app.ui.arranque.ArranqueScreen
import com.smartassign.app.ui.destinos.MallaLineaScreen
import com.smartassign.app.ui.destinos.PanelBolsonScreen
import com.smartassign.app.ui.destinos.PanelPlantaScreen
import com.smartassign.app.ui.destinos.SinLineaScreen
import com.smartassign.app.ui.login.LoginScreen
import com.smartassign.app.ui.navegacion.Rutas
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

@Composable
fun GrafoDeNavegacion(controlador: NavHostController = rememberNavController()) {
    NavHost(navController = controlador, startDestination = Rutas.ARRANQUE) {
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

/** El splash nunca queda en el historial — al decidir su destino, se reemplaza. */
private fun navegarYReemplazarArranque(controlador: NavHostController, ruta: String) {
    controlador.navigate(ruta) { popUpTo(Rutas.ARRANQUE) { inclusive = true } }
}

/** Tras autenticar, ni login ni PIN quedan en el historial — "atrás" no debe volver a pedir credenciales. */
private fun navegarYLimpiarHistorialDeSesion(controlador: NavHostController, destino: String) {
    controlador.navigate(destino) { popUpTo(Rutas.ARRANQUE) { inclusive = true } }
}
