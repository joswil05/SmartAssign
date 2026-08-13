package com.smartassign.app.data.cache

import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.sesion.PurgaCacheLocal
import javax.inject.Inject
import javax.inject.Singleton

/** Resultado de intentar cachear a alguien — nunca una excepción silenciosa. */
sealed interface ResultadoCacheo {
    /** Guardada. `restriccionesGuardadas = false` cuando el rol es Coordinador (00 §D3). */
    data class Guardada(val restriccionesGuardadas: Boolean) : ResultadoCacheo

    /** No hay alcance abierto: nadie declaró para qué línea y rol sirve esta caché. */
    data object SinAlcance : ResultadoCacheo
}

const val ROL_COORDINADOR = "coordinador"

/**
 * UT-E13.2 (docs/PROGRESO.md): la única puerta de escritura de la caché
 * (00 §D3). `CacheDao` sigue existiendo, pero quien guarda datos de
 * personas pasa por aquí — es donde viven las tres reglas de la tabla de
 * D3 que E13.1 dejó explícitamente fuera:
 *
 * | Regla de D3 | Cómo se impone aquí |
 * |---|---|
 * | **Alcance:** solo su línea, *"nunca el padrón completo"* | No se puede escribir sin un alcance abierto ([abrirAlcance]); y el alcance es del almacén entero, no de cada fila |
 * | **Purga:** al cerrar sesión, cerrar turno, reasignar línea, inactividad | [purgar] para las tres primeras; [abrirAlcance] purga sola cuando el alcance cambia |
 * | **Coordinador:** su dispositivo *"no cachea restricciones médicas"* | [guardar] las descarta si el rol es Coordinador — no las guarda "por si acaso" |
 *
 * **Por qué el alcance es del almacén y no de cada persona.** Si cada
 * fila llevara su línea, "cachear a alguien de otra línea" sería un error
 * posible que alguien tendría que acordarse de no cometer, y una revisión
 * tendría que auditar cada llamada. Siendo del almacén, la pregunta
 * *"¿puede acabar aquí el padrón de 160 personas?"* se responde sola: la
 * caché sirve a UNA línea y a UN rol, y en cuanto cualquiera de los dos
 * cambia se vacía antes de aceptar nada. Es el mismo criterio con el que
 * `PlantaHub` (E12.1) no expone ningún método invocable: la garantía es
 * la ausencia de un camino, no una comprobación que se pueda olvidar.
 *
 * Lo que esta UT **no** hace: decidir CUÁNDO se dispara la purga por
 * inactividad. D3 la llama *"configurable"* y ningún documento fija el
 * número — mismo criterio que todos los umbrales *a definir* de `04 §9`
 * (`fn_NivelFatiga`, `sp_CerrarLote`…): no se inventa. [purgar] es el
 * mecanismo, listo para que lo llame quien tenga el temporizador.
 */
@Singleton
class CachePersonalRepositorio @Inject constructor(private val dao: CacheDao) : PurgaCacheLocal {

    /**
     * Declara para qué rol y línea sirve la caché a partir de ahora.
     * Si cualquiera de los dos cambia respecto de lo que había, **purga
     * todo antes de abrir el alcance nuevo** — es la purga "al reasignar
     * línea" de D3, y también cubre el cambio de usuario en un teléfono
     * compartido (D6: "el teléfono se trata como compartido por línea").
     *
     * @return `true` si hubo purga.
     */
    suspend fun abrirAlcance(rol: String, lineaId: Int?, ahora: Long = System.currentTimeMillis()): Boolean {
        val actual = dao.alcance()
        val cambio = actual == null || actual.rol != rol || actual.lineaId != lineaId
        if (cambio) dao.purgarTodoYAlcance()
        dao.guardarAlcance(AlcanceCacheEntity(rol = rol, lineaId = lineaId, abiertoEn = ahora))
        return cambio
    }

    /**
     * Guarda a una persona dentro del alcance vigente.
     *
     * Un Coordinador **sí** puede cachear a una persona (nombre, ficha,
     * categoría — lo que §12.2 exige para confirmar identidad), pero
     * nunca sus restricciones médicas: D3 es literal — *"su dispositivo
     * no cachea restricciones médicas de las 10 líneas: las consulta en
     * línea bajo demanda"*, porque su alcance son 160 personas y
     * precargarlo *"convertiría un teléfono extraviado en una fuga del
     * padrón médico completo"*.
     */
    suspend fun guardar(persona: PersonaConfirmacion, ahora: Long = System.currentTimeMillis()): ResultadoCacheo {
        val alcance = dao.alcance() ?: return ResultadoCacheo.SinAlcance

        val esCoordinador = alcance.rol == ROL_COORDINADOR
        val restricciones = if (esCoordinador) emptyList() else persona.restriccionesMedicas

        dao.guardar(
            PersonaCacheadaEntity(
                personalId = persona.personalId,
                ficha = persona.ficha,
                nombreCompleto = persona.nombreCompleto,
                categoria = persona.categoria,
                cacheadoEn = ahora
            ),
            restricciones
        )
        return ResultadoCacheo.Guardada(restriccionesGuardadas = !esCoordinador)
    }

    /**
     * Lee del cache. Devuelve `null` si no está — que es distinto de
     * "está y no tiene restricciones" (§12.4, honestidad del dato).
     *
     * ⚠ Para un Coordinador, `restriccionesMedicas` vendrá **siempre
     * vacía** porque nunca se guardaron. Quien presente esta información
     * debe consultar en línea bajo demanda (D3) en vez de mostrar
     * "sin restricciones", que sería exactamente la mentira que §12.2
     * quiere impedir. La distinción se expone con [alcanceCacheaMedicos].
     */
    suspend fun porFicha(ficha: String): PersonaConfirmacion? {
        val fila = dao.personaPorFicha(ficha) ?: return null
        return PersonaConfirmacion(
            personalId = fila.personalId,
            nombreCompleto = fila.nombreCompleto,
            ficha = fila.ficha,
            categoria = fila.categoria,
            restriccionesMedicas = dao.restriccionesDe(fila.personalId)
        )
    }

    /**
     * `false` cuando el alcance vigente es de Coordinador (o no hay
     * alcance): en ese caso una lista de restricciones vacía **no
     * significa "no tiene"**, significa "aquí no se guardan".
     */
    suspend fun alcanceCacheaMedicos(): Boolean {
        val alcance = dao.alcance() ?: return false
        return alcance.rol != ROL_COORDINADOR
    }

    suspend fun alcanceVigente(): AlcanceCacheEntity? = dao.alcance()

    /** Purga de D3 — cierre de sesión, cierre de turno, inactividad. */
    override suspend fun purgar() = dao.purgarTodoYAlcance()
}
