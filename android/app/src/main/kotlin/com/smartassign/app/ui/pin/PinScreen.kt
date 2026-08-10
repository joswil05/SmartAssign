package com.smartassign.app.ui.pin

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeBody
import com.smartassign.app.ui.theme.TypeCaption
import com.smartassign.app.ui.theme.TypeTitle

@Composable
fun PinScreen(
    onAutenticado: (String) -> Unit,
    onVolverALogin: () -> Unit,
    viewModel: PinViewModel = hiltViewModel()
) {
    val estado by viewModel.uiState.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .testTag("pantalla-pin")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center,
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text("Ingresa tu PIN", style = TypeTitle)
        viewModel.nombreUsuario?.let {
            Text(it, style = TypeBody, modifier = Modifier.padding(top = Spacing.xs))
        }

        OutlinedTextField(
            value = estado.pin,
            onValueChange = viewModel::onPinChange,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword),
            singleLine = true,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.lg)
                .testTag("pin-campo")
        )

        if (estado.error != null) {
            Text(
                text = estado.error!!,
                style = TypeCaption,
                color = ColorPeligro,
                modifier = Modifier.padding(top = Spacing.md).testTag("pin-error")
            )
        }

        if (estado.enviando) {
            CircularProgressIndicator(modifier = Modifier.padding(top = Spacing.lg).testTag("pin-cargando"))
        } else {
            Button(
                onClick = { viewModel.verificar(onAutenticado, onVolverALogin) },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = Spacing.lg)
                    .height(TouchTarget.accionPrimaria)
                    .testTag("pin-verificar")
            ) {
                Text("Desbloquear")
            }
        }
    }
}
