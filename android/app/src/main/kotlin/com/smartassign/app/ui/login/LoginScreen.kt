package com.smartassign.app.ui.login

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
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.ui.theme.ColorPeligro
import com.smartassign.app.ui.theme.Spacing
import com.smartassign.app.ui.theme.TouchTarget
import com.smartassign.app.ui.theme.TypeCaption
import com.smartassign.app.ui.theme.TypeTitle

@Composable
fun LoginScreen(
    onAutenticado: (String) -> Unit,
    viewModel: LoginViewModel = hiltViewModel()
) {
    val estado by viewModel.uiState.collectAsState()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .testTag("pantalla-login")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center
    ) {
        Text("Iniciar sesión", style = TypeTitle)

        OutlinedTextField(
            value = estado.username,
            onValueChange = viewModel::onUsernameChange,
            label = { Text("Usuario") },
            singleLine = true,
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.lg)
                .testTag("login-usuario")
        )

        OutlinedTextField(
            value = estado.password,
            onValueChange = viewModel::onPasswordChange,
            label = { Text("Contraseña") },
            singleLine = true,
            visualTransformation = PasswordVisualTransformation(),
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
            modifier = Modifier
                .fillMaxWidth()
                .padding(top = Spacing.md)
                .testTag("login-password")
        )

        if (estado.error != null) {
            Text(
                text = estado.error!!,
                style = TypeCaption,
                color = ColorPeligro,
                modifier = Modifier.padding(top = Spacing.md).testTag("login-error")
            )
            estado.siguientePaso?.let {
                Text(text = it, style = TypeCaption, modifier = Modifier.padding(top = Spacing.xs))
            }
        }

        if (estado.enviando) {
            CircularProgressIndicator(
                modifier = Modifier.padding(top = Spacing.lg).testTag("login-cargando")
            )
        } else {
            Button(
                onClick = { viewModel.iniciarSesion(onAutenticado) },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = Spacing.lg)
                    .height(TouchTarget.accionPrimaria)
                    .testTag("login-entrar"),
            ) {
                Text("Entrar")
            }
        }
    }
}
