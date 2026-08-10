package com.smartassign.app.data.personal

class FakePersonalRepositorio : PersonalRepositorio {
    var resultado: ResultadoPersonal = ResultadoPersonal.NoEncontrado

    override suspend fun porFicha(ficha: String): ResultadoPersonal = resultado
}
