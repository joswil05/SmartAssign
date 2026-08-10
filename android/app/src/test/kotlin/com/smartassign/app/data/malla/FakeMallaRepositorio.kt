package com.smartassign.app.data.malla

class FakeMallaRepositorio : MallaRepositorio {
    var resultado: ResultadoMalla = ResultadoMalla.Ok(emptyList())
    var ultimaLineaIdConsultada: Int? = null

    override suspend fun puestosDeLinea(lineaId: Int): ResultadoMalla {
        ultimaLineaIdConsultada = lineaId
        return resultado
    }
}
