package com.smartassign.app.data.version

interface VerificadorVersionRepositorio {
    suspend fun verificar(): ResultadoVersion
}
