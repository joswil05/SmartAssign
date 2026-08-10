package com.smartassign.app.ui.sesion

/**
 * Traduce los códigos estables de `CodigosRechazoSesion` (backend) a
 * lenguaje de planta (§12.4: "nunca códigos de error"). El código nunca
 * se muestra — solo gobierna cuál texto aparece.
 */
object MensajesSesion {

    /**
     * 02 §1.1: el nodo `⛔ [Error auth]` es uno solo — "mensaje claro sin
     * detalle de qué falló". Todo rechazo de `/api/auth/login`, sea cual
     * sea la causa real (usuario inexistente, contraseña equivocada,
     * cuenta inactiva/bloqueada, origen AD no disponible), cae en el
     * mismo texto a propósito: distinguirlos le diría a quien intenta
     * entrar CUÁL de las dos cosas falló, y el diagrama no deja esa
     * puerta abierta.
     */
    const val ERROR_LOGIN = "Usuario o contraseña incorrectos."
    const val ERROR_LOGIN_SIGUIENTE_PASO = "Verifica tus datos e inténtalo de nuevo."

    const val SIN_CONEXION = "No se pudo llegar al servidor."
    const val SIN_CONEXION_SIGUIENTE_PASO = "Revisa que estés en la red de planta y vuelve a intentar."

    fun paraPin(codigo: String): String = when (codigo) {
        "PIN_INCORRECTO" -> "PIN incorrecto."
        "PIN_MAX_INTENTOS_SESION_CERRADA" -> "Demasiados intentos. Inicia sesión de nuevo."
        "PIN_NO_CONFIGURADO" -> "Este usuario no tiene PIN configurado. Inicia sesión de nuevo."
        else -> "No se pudo verificar el PIN."
    }

    /** 04 §6.4: al tercer PIN fallido, el servidor ya cerró la sesión — el cliente solo obedece. */
    fun pinFuerzaLogin(codigo: String): Boolean =
        codigo == "PIN_MAX_INTENTOS_SESION_CERRADA" || codigo == "PIN_NO_CONFIGURADO"
}
