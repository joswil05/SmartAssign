package com.smartassign.app.ui.estado

/**
 * Los cuatro estados de pantalla — docs/03_UIUX_BRIEF.md §3.11, fuente §12.4.
 *
 * `[REGLA DURA]` de interfaz: cargando, vacío legítimo, fuera de operación
 * y error nunca pueden verse igual entre sí ni confundirse con el
 * contenido real (principio 1, §1.3). La distinción entre **cargando** y
 * **vacío** es la más importante del documento (§12.4, §3.11): una línea
 * que aún no responde y una línea sin nadie asignado no pueden lucir
 * iguales, o el supervisor termina reasignando personal que ya estaba
 * colocado.
 *
 * `Listo` (con datos) no es uno de "los cuatro" — es el estado normal de
 * una pantalla que ya cargó contenido real. Se declara aquí junto a ellos
 * para que el `when` que dibuja cualquier pantalla ([PantallaConEstado])
 * sea exhaustivo, sin un `else` que esconda un estado sin tratamiento
 * propio.
 */
sealed interface EstadoPantalla<out T> {

    /** Esqueleto con la forma del contenido real — nunca pantalla en blanco, nunca un círculo girando. */
    data object Cargando : EstadoPantalla<Nothing>

    /** El contenido real, ya cargado. */
    data class Listo<T>(val datos: T) : EstadoPantalla<T>

    /**
     * No hay nada que mostrar y es correcto que no lo haya — p. ej. "ningún
     * puesto en fatiga ahora mismo". Siempre lleva el siguiente paso, nunca
     * solo la ausencia.
     */
    data class VacioLegitimo(
        val mensaje: String,
        val siguientePaso: String
    ) : EstadoPantalla<Nothing>

    /**
     * Tratamiento propio, ni libre ni ocupado (§5.3 del brief, C11). No es
     * un error ni una ausencia de datos: es que el dato no aplica hoy.
     */
    data class FueraDeOperacion(
        val motivo: String
    ) : EstadoPantalla<Nothing>

    /**
     * Causa concreta + acción concreta, en lenguaje de planta. Nunca un
     * código de error ni un mensaje genérico (§12.4).
     */
    data class Error(
        val causa: String,
        val accionSugerida: String
    ) : EstadoPantalla<Nothing>
}
