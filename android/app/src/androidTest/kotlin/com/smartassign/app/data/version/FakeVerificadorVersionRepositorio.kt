package com.smartassign.app.data.version

/** Repositorio guionado — para probar pantallas de Compose sin red real. */
class FakeVerificadorVersionRepositorio : VerificadorVersionRepositorio {
    var resultado: ResultadoVersion = ResultadoVersion.Compatible

    override suspend fun verificar(): ResultadoVersion = resultado
}
