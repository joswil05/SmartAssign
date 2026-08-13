package com.smartassign.app.data.version

import com.smartassign.app.BuildConfig
import com.smartassign.app.data.red.AuthApi
import com.smartassign.app.data.sesion.SesionLocal
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.IOException
import javax.inject.Inject

/**
 * UT-E14.6 (00 §F3, 04 §10.1). <c>AuthApi.versionActual()</c> es anónima
 * (`InterceptorAutorizacion`, sin sesión) — se puede llamar antes de
 * iniciar sesión, que es justo cuando el §F3 exige comprobar. La URL de
 * descarga se arma con <see cref="SesionLocal.servidorUrl"/> (ya
 * persistida desde la alta por QR, E1) más la ruta fija del endpoint de
 * descarga — nunca se expone <c>ruta_apk</c> (ruta de disco del
 * servidor) directo al cliente.
 */
class VerificadorVersionRepositorioImpl @Inject constructor(
    private val api: AuthApi,
    private val local: SesionLocal
) : VerificadorVersionRepositorio {

    override suspend fun verificar(): ResultadoVersion = withContext(Dispatchers.IO) {
        val base = local.servidorUrl() ?: return@withContext ResultadoVersion.SinDatoDelServidor
        val descargaUrl = base.trimEnd('/') + "/api/version-app/apk"

        try {
            val respuesta = api.versionActual()
            val cuerpo = respuesta.body()
            // !isSuccessful cubre el 404 real de "ninguna versión
            // publicada todavía" (§1.3) — nunca se bloquea por falta de
            // dato, solo por un dato real que diga que hace falta.
            if (!respuesta.isSuccessful || cuerpo == null) ResultadoVersion.SinDatoDelServidor
            else evaluarVersion(BuildConfig.VERSION_CODE, cuerpo, descargaUrl)
        } catch (_: IOException) {
            ResultadoVersion.SinDatoDelServidor
        }
    }
}
