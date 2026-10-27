package com.smartassign.app.ui.login

import android.content.Intent
import android.net.Uri
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.imePadding
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.testTag
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.hilt.navigation.compose.hiltViewModel
import com.smartassign.app.data.version.ResultadoVersion
import com.smartassign.app.ui.theme.ColorPeligroTexto
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
    val contexto = LocalContext.current
    fun abrirDescarga(url: String) = contexto.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse(url)))

    val bloqueo = estado.resultadoVersion as? ResultadoVersion.Bloqueada
    if (bloqueo != null) {
        // 00 §F3: por debajo del mínimo, se bloquea de verdad — el
        // formulario ni se muestra, no solo se deshabilita.
        Column(
            modifier = Modifier
                .fillMaxSize()
                .verticalScroll(rememberScrollState())
                .imePadding()
                .testTag("pantalla-login-bloqueada")
                .padding(Spacing.xl),
            verticalArrangement = Arrangement.Center
        ) {
            Text("Actualización requerida", style = TypeTitle)
            Text(
                text = "Esta versión de la app ya no es compatible con el servidor. Instala ${bloqueo.versionNombre} para continuar.",
                style = TypeCaption,
                modifier = Modifier.padding(top = Spacing.md)
            )
            Button(
                onClick = { abrirDescarga(bloqueo.descargaUrl) },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(top = Spacing.lg)
                    .heightIn(min = TouchTarget.accionPrimaria)
                    .testTag("login-descargar-actualizacion-bloqueante"),
            ) {
                Text("Descargar actualización")
            }
        }
        return
    }

    // UT-E15.2 (08 §P0-3): sin `verticalScroll` el contenido no cabía en
    // pantalla corta —titulo + banner de actualizacion + dos campos + dos
    // lineas de error + boton— y sin `imePadding()` el teclado tapaba el
    // boton primario, dejandolo inalcanzable. Con scroll, `Arrangement.Center`
    // sigue centrando mientras cabe y pasa a alinear arriba cuando desborda,
    // que es exactamente el comportamiento que se quiere.
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .imePadding()
            .testTag("pantalla-login")
            .padding(Spacing.xl),
        verticalArrangement = Arrangement.Center
    ) {
        Text("Iniciar sesión", style = TypeTitle)

        (estado.resultadoVersion as? ResultadoVersion.ActualizacionDisponible)?.let { disponible ->
            // No bloquea — "se ofrece la actualización pero no se
            // impone" (00 §F3, literal): el formulario sigue debajo, usable.
            Column(modifier = Modifier.fillMaxWidth().padding(top = Spacing.md).testTag("login-banner-actualizacion-disponible")) {
                Text("Hay una versión nueva disponible: ${disponible.versionNombre}", style = TypeCaption)
                Button(
                    onClick = { abrirDescarga(disponible.descargaUrl) },
                    modifier = Modifier
                        .padding(top = Spacing.xs)
                        .heightIn(min = TouchTarget.accionTerciaria)
                        .testTag("login-descargar-actualizacion-disponible"),
                ) {
                    Text("Descargar")
                }
            }
        }

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
                color = ColorPeligroTexto,
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
                    .heightIn(min = TouchTarget.accionPrimaria)
                    .testTag("login-entrar"),
            ) {
                Text("Entrar")
            }
        }
    }
}
