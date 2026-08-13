package com.smartassign.app.data.version

/** Repositorio guionado — para probar ViewModels sin red ni Android (JVM puro). */
class FakeVerificadorVersionRepositorio : VerificadorVersionRepositorio {
    var resultado: ResultadoVersion = ResultadoVersion.Compatible

    override suspend fun verificar(): ResultadoVersion = resultado
}
