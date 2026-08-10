package com.smartassign.app.ui.asignacion

import com.smartassign.app.MainDispatcherRule
import com.smartassign.app.data.asignacion.FakeAsignacionRepositorio
import com.smartassign.app.data.asignacion.ResultadoAsignar
import com.smartassign.app.data.asignacion.ResultadoSugerencia
import com.smartassign.app.data.malla.PuestoMalla
import com.smartassign.app.data.personal.FakePersonalRepositorio
import com.smartassign.app.data.personal.PersonaConfirmacion
import com.smartassign.app.data.personal.ResultadoPersonal
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertTrue
import org.junit.Rule
import org.junit.Test

/**
 * La integración entre E6.5/E6.6/E6.7/E6.8 que faltaba antes de PC-3
 * (07 §6): ficha → identidad → sugerencia → confirmación. Cada resultado
 * que este ViewModel expone es el que devuelve el servidor real (o su
 * doble aquí), nunca uno inventado.
 */
class FlujoAsignacionViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    private val persona = PersonaConfirmacion(
        personalId = 42, nombreCompleto = "María López Hernández", ficha = "4821",
        categoria = "operario", restriccionesMedicas = emptyList()
    )

    private fun puesto(id: Int, codigo: String, tipo: String) = PuestoMalla(
        id = id, codigo = codigo, nombrePuesto = codigo, tipo = tipo, situacion = "descubierto",
        ocupante = null, indicadorMedico = 0, microCopia = "micro-copia de prueba"
    )

    private fun vm(
        resultadoPersona: ResultadoPersonal = ResultadoPersonal.Ok(persona),
        resultadoSugerencia: ResultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 7, nivel = 1),
        resultadoAsignar: ResultadoAsignar = ResultadoAsignar.SinConexion
    ): Triple<FlujoAsignacionViewModel, FakePersonalRepositorio, FakeAsignacionRepositorio> {
        val personalRepo = FakePersonalRepositorio().apply { resultado = resultadoPersona }
        val asignacionRepo = FakeAsignacionRepositorio().apply {
            this.resultadoSugerencia = resultadoSugerencia
            this.resultadoAsignar = resultadoAsignar
        }
        return Triple(FlujoAsignacionViewModel(personalRepo, asignacionRepo), personalRepo, asignacionRepo)
    }

    @Test
    fun empieza_cargando() {
        val (vm, _, _) = vm()
        assertTrue(vm.estado.value is EstadoFlujoAsignacion.Cargando)
    }

    @Test
    fun sugerencia_exitosa_resuelve_el_destino_desde_la_malla_ya_cargada() {
        val (vm, _, _) = vm(resultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 7, nivel = 2))

        vm.iniciar("4821", listOf(puesto(7, "L4-R03", "rotativo")))

        val estado = vm.estado.value
        assertTrue(estado is EstadoFlujoAsignacion.ListoParaConfirmar)
        val listo = estado as EstadoFlujoAsignacion.ListoParaConfirmar
        assertEquals(42, listo.personalId)
        assertEquals(7, listo.puestoId)
        assertEquals("L4-R03", listo.destinoPuesto)
        assertEquals("Rotativo", listo.destinoTipo)
    }

    @Test
    fun sugerencia_exitosa_sin_el_puesto_en_la_lista_local_usa_un_texto_de_respaldo() {
        // La malla en pantalla puede estar un instante desactualizada; el
        // texto de respaldo nunca bloquea la confirmación real, que de
        // todas formas la decide el servidor por el id, no por el texto.
        val (vm, _, _) = vm(resultadoSugerencia = ResultadoSugerencia.Ok(puestoId = 99, nivel = 1))

        vm.iniciar("4821", emptyList())

        val listo = vm.estado.value as EstadoFlujoAsignacion.ListoParaConfirmar
        assertEquals("Puesto 99", listo.destinoPuesto)
    }

    @Test
    fun cada_llamada_a_iniciar_genera_una_clave_de_idempotencia_distinta() {
        val (vm, _, _) = vm()

        vm.iniciar("4821", emptyList())
        val primeraClave = (vm.estado.value as EstadoFlujoAsignacion.ListoParaConfirmar).idempotencyKey

        vm.iniciar("4821", emptyList())
        val segundaClave = (vm.estado.value as EstadoFlujoAsignacion.ListoParaConfirmar).idempotencyKey

        assertNotEquals(primeraClave, segundaClave)
    }

    @Test
    fun ficha_sin_dueno_muestra_error_con_causa_y_accion() {
        val (vm, _, _) = vm(resultadoPersona = ResultadoPersonal.NoEncontrado)

        vm.iniciar("no-existe", emptyList())

        val estado = vm.estado.value as EstadoFlujoAsignacion.Error
        assertTrue(estado.causa.contains("no-existe"))
        assertTrue(estado.accionSugerida.isNotBlank())
    }

    @Test
    fun sin_conexion_al_resolver_la_persona_muestra_error_no_se_queda_cargando() {
        val (vm, _, _) = vm(resultadoPersona = ResultadoPersonal.SinConexion)

        vm.iniciar("4821", emptyList())

        assertTrue(vm.estado.value is EstadoFlujoAsignacion.Error)
    }

    @Test
    fun sin_puestos_libres_expone_el_mensaje_real_del_servidor_sin_reescribirlo() {
        val (vm, _, _) = vm(
            resultadoSugerencia = ResultadoSugerencia.SinSugerencia("SIN_PUESTOS_LIBRES", "No hay puestos rotativos libres compatibles en L4.")
        )

        vm.iniciar("4821", emptyList())

        val estado = vm.estado.value as EstadoFlujoAsignacion.Error
        assertEquals("No hay puestos rotativos libres compatibles en L4.", estado.causa)
    }

    @Test
    fun confirmar_en_exito_deja_el_estado_confirmado_con_el_id_de_asignacion() {
        val (vm, _, _) = vm(resultadoAsignar = ResultadoAsignar.Ok(asignacionId = 555L))
        vm.iniciar("4821", listOf(puesto(7, "L4-R03", "rotativo")))

        vm.confirmar()

        val estado = vm.estado.value
        assertTrue(estado is EstadoFlujoAsignacion.Confirmado)
        assertEquals(555L, (estado as EstadoFlujoAsignacion.Confirmado).asignacionId)
    }

    @Test
    fun confirmar_reenvia_la_misma_clave_de_idempotencia_que_se_genero_al_preparar_la_confirmacion() {
        val (vm, _, asignacionRepo) = vm(resultadoAsignar = ResultadoAsignar.Ok(asignacionId = 1L))
        vm.iniciar("4821", listOf(puesto(7, "L4-R03", "rotativo")))
        val claveEsperada = (vm.estado.value as EstadoFlujoAsignacion.ListoParaConfirmar).idempotencyKey

        vm.confirmar()

        assertEquals(claveEsperada, asignacionRepo.ultimaPeticionAsignar?.idempotencyKey)
        assertEquals(7, asignacionRepo.ultimaPeticionAsignar?.puestoId)
        assertEquals(42, asignacionRepo.ultimaPeticionAsignar?.personalId)
    }

    @Test
    fun confirmar_rechazado_expone_el_mensaje_nominal_del_servidor() {
        // 00 §B1: "[Nombre] acaba de ser registrado en L4 · Puesto 3 por otro supervisor" — literal, no genérico.
        val (vm, _, _) = vm(
            resultadoAsignar = ResultadoAsignar.Rechazado("PUESTO_OCUPADO", "María López Hernández acaba de ser registrada en L4 · Puesto 3 por otro supervisor.")
        )
        vm.iniciar("4821", listOf(puesto(7, "L4-R03", "rotativo")))

        vm.confirmar()

        val estado = vm.estado.value as EstadoFlujoAsignacion.RechazadoAlConfirmar
        assertEquals("María López Hernández acaba de ser registrada en L4 · Puesto 3 por otro supervisor.", estado.mensaje)
    }

    @Test
    fun confirmar_sin_haber_llegado_a_listo_para_confirmar_no_hace_nada() {
        val (vm, _, asignacionRepo) = vm()
        // El estado sigue en Cargando: nunca se resolvió una sugerencia.

        vm.confirmar()

        assertTrue(vm.estado.value is EstadoFlujoAsignacion.Cargando)
        assertEquals(null, asignacionRepo.ultimaPeticionAsignar)
    }
}
