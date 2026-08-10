package com.smartassign.app.data.malla

class FakeMallaRepositorio : MallaRepositorio {
    var resultado: ResultadoMalla = ResultadoMalla.Ok(emptyList())

    override suspend fun puestosDeLinea(lineaId: Int): ResultadoMalla = resultado
}
