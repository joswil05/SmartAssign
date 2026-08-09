package com.smartassign.app

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import com.smartassign.app.ui.theme.SmartAssignTheme
import com.smartassign.app.ui.theme.Spacing

/**
 * Punto de entrada. El flujo real de arranque —alta de dispositivo por
 * QR, sesión, PIN, resolución de rol y línea— se construye en la
 * etapa E6 (docs/02_FLUJOS_DE_APP.md §1). Esto es el esqueleto que
 * UT-E0.3 exige verificar con `gradlew assembleDebug`.
 */
class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()
        setContent {
            SmartAssignTheme {
                ArranqueScreen()
            }
        }
    }
}

@Composable
fun ArranqueScreen() {
    Scaffold { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(Spacing.lg),
            verticalArrangement = Arrangement.Center,
            horizontalAlignment = Alignment.CenterHorizontally
        ) {
            Text(text = "SmartAssign", style = MaterialTheme.typography.displayLarge)
            Text(
                text = "Esqueleto en construcción — ver docs/PROGRESO.md",
                style = MaterialTheme.typography.bodyLarge
            )
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun ArranquePreview() {
    SmartAssignTheme {
        ArranqueScreen()
    }
}
