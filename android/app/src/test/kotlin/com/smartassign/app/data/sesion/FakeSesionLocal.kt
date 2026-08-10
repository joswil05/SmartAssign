package com.smartassign.app.data.sesion

/** En memoria, sin Android Keystore — para pruebas de JVM puro. */
class FakeSesionLocal(private val deviceIdFijo: String = "device-jvm-test") : SesionLocal {
    private var servidor: String? = null
    private var tokensGuardados: TokensGuardados? = null
    private var identidadGuardada: IdentidadGuardada? = null

    override fun deviceId(): String = deviceIdFijo
    override fun servidorUrl(): String? = servidor
    override fun guardarServidorUrl(url: String) { servidor = url }
    override fun tokens(): TokensGuardados? = tokensGuardados
    override fun guardarTokens(tokens: TokensGuardados) { tokensGuardados = tokens }
    override fun identidad(): IdentidadGuardada? = identidadGuardada
    override fun guardarIdentidad(identidad: IdentidadGuardada) { identidadGuardada = identidad }
    override fun limpiarSesion() {
        tokensGuardados = null
        identidadGuardada = null
    }
}
