package com.smartassign.app.data.sesion

/** Repositorio guionado — para probar ViewModels sin red ni Android (JVM puro). */
class FakeSesionRepositorio : SesionRepositorio {
    var servidorVerificado = true
    var ultimaUrlVerificada: String? = null
    var servidorGuardadoUrl: String? = null
    var configurado = false
    var sesionGuardada = false
    var resultadoLogin: ResultadoAuth = ResultadoAuth.Rechazo("SIN_GUION")
    var resultadoRenovar: ResultadoAuth = ResultadoAuth.Rechazo("SIN_GUION")
    var resultadoPin: ResultadoAuth = ResultadoAuth.Rechazo("SIN_GUION")
    var quienSoyResultado: QuienSoy? = null
    var identidadGuardadaValor: IdentidadGuardada? = null
    var seCerroSesion = false

    override suspend fun verificarServidor(url: String): Boolean {
        ultimaUrlVerificada = url
        return servidorVerificado
    }

    override fun guardarServidor(url: String) {
        servidorGuardadoUrl = url
    }

    override fun servidorConfigurado(): Boolean = configurado
    override fun haySesionGuardada(): Boolean = sesionGuardada
    override suspend fun iniciarSesion(username: String, password: String): ResultadoAuth = resultadoLogin
    override suspend fun renovarSesion(): ResultadoAuth = resultadoRenovar
    override suspend fun reentrarConPin(pin: String): ResultadoAuth = resultadoPin
    override suspend fun quienSoy(): QuienSoy? = quienSoyResultado
    override suspend fun cerrarSesion() { seCerroSesion = true }
    override fun identidadGuardada(): IdentidadGuardada? = identidadGuardadaValor
}
