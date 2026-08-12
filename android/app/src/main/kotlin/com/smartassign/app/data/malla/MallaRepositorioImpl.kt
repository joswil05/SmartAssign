package com.smartassign.app.data.malla

import com.smartassign.app.data.red.MallaApi
import com.smartassign.app.data.red.PuestoMallaResponse
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import java.io.IOException
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class MallaRepositorioImpl @Inject constructor(private val api: MallaApi) : MallaRepositorio {

    override suspend fun puestosDeLinea(lineaId: Int): ResultadoMalla = withContext(Dispatchers.IO) {
        try {
            val respuesta = api.puestosDeLinea(lineaId)
            when {
                respuesta.code() == 403 -> ResultadoMalla.SinAlcance
                respuesta.isSuccessful -> ResultadoMalla.Ok((respuesta.body() ?: emptyList()).map(::aDominio))
                else -> ResultadoMalla.SinAlcance
            }
        } catch (_: IOException) {
            ResultadoMalla.SinConexion
        }
    }

    private fun aDominio(r: PuestoMallaResponse) = PuestoMalla(
        id = r.id,
        codigo = r.codigo,
        nombrePuesto = r.nombrePuesto,
        tipo = r.tipo,
        situacion = r.situacion,
        ocupante = r.ocupante?.let { OcupantePuesto(it.personalId, it.nombreCompleto, it.ficha, it.categoria, it.dobleTurno) },
        indicadorMedico = r.indicadorMedico,
        microCopia = r.microCopia,
        nivelFatiga = r.nivelFatiga,
        excesoFatiga = r.excesoFatiga
    )
}
