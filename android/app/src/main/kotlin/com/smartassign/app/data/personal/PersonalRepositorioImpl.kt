package com.smartassign.app.data.personal

import com.smartassign.app.data.red.PersonalApi
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class PersonalRepositorioImpl @Inject constructor(private val api: PersonalApi) : PersonalRepositorio {

    override suspend fun porFicha(ficha: String): ResultadoPersonal = withContext(Dispatchers.IO) {
        try {
            val respuesta = api.porFicha(ficha)
            when {
                respuesta.code() == 404 -> ResultadoPersonal.NoEncontrado
                respuesta.isSuccessful -> respuesta.body()?.let { ResultadoPersonal.Ok(aDominio(it)) } ?: ResultadoPersonal.NoEncontrado
                else -> ResultadoPersonal.NoEncontrado
            }
        } catch (_: IOException) {
            ResultadoPersonal.SinConexion
        }
    }

    private fun aDominio(r: com.smartassign.app.data.red.PersonalPorFichaResponse) = PersonaConfirmacion(
        personalId = r.personalId,
        nombreCompleto = r.nombreCompleto,
        ficha = r.ficha,
        categoria = r.categoria,
        restriccionesMedicas = r.restriccionesMedicas
    )
}
