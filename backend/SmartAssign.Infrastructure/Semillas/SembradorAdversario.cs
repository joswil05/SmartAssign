using Microsoft.EntityFrameworkCore;
using SmartAssign.Domain.Entities;
using SmartAssign.Infrastructure.Persistence;

namespace SmartAssign.Infrastructure.Semillas;

/// <summary>
/// UT-E3.5 (docs/PROGRESO.md): los 16 escenarios de la semilla adversaria
/// (07 §4.4). <b>Nunca se llama desde una migración ni desde
/// <c>OnModelCreating</c></b> — a diferencia de <c>DatosEstructurales</c>/
/// <c>DatosCatalogo</c> (que sí van a producción), esto es
/// deliberadamente lo opuesto: "ninguna fila simulada llega a
/// producción" (00 §G2). Se invoca a mano, una vez, contra una base de
/// desarrollo — ver herramientas/SemillaAdversariaCli.
///
/// Todas las filas que crea quedan marcadas <c>origen_dato</c> ≠ 'real'
/// (Personal, RestriccionMedica, Puesto y AusenciaJustificada), que es lo
/// que hace computables <c>sp_VerificarSinDatosSimulados</c> y
/// <c>sp_PurgarDatosSimulados</c> (UT-E14.7, 07 §4.4).
///
/// <b>Lo que la purga NO puede deshacer</b>, y por eso una base que ha
/// corrido esta semilla nunca se promueve a producción: además de
/// insertar filas, la semilla MODIFICA filas reales en sitio sin guardar
/// el valor anterior — <c>Linea.MinimoOperarios</c> (escenario 16, B5) y
/// <c>Personal.Situacion</c>/<c>LineaFisicaActual</c> (escenarios 14-16).
/// Producción se construye desde migraciones + importador real, nunca
/// limpiando una base de desarrollo.
/// </summary>
public class SembradorAdversario(SmartAssignDbContext db)
{
    public async Task<string> SembrarAsync(int usuarioId, CancellationToken ct = default)
    {
        var informe = new List<string>();

        var capacidad = await db.CapacidadesFisicas.SingleAsync(c => c.Codigo == "levantar_carga", ct);

        // ── Escenarios 1-4: restricción médica vigente / caducada / permanente / futura ──
        var puestoRestriccion = await CrearOUsarPuestoAsync(1, "SIM-P01", "Puesto con capacidad exigida (simulado)",
            horasEnPuesto: 2, ct: ct);
        await CrearPuestoCapacidadAsync(puestoRestriccion.Id, capacidad.Id, ct);

        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var simVigente = await CrearOUsarPersonaSimuladaAsync("SIM-0001", "Escenario 1 — Restricción vigente", 1, ct);
        await CrearRestriccionAsync(simVigente.Id, capacidad.Id, hoy.AddDays(-30), hoy.AddDays(30), usuarioId, ct);
        informe.Add("1. Restricción VIGENTE que choca con el puesto habitual → debe bloquear (SIM-0001 / SIM-P01)");

        var simCaducada = await CrearOUsarPersonaSimuladaAsync("SIM-0002", "Escenario 2 — Restricción caducada", 1, ct);
        await CrearRestriccionAsync(simCaducada.Id, capacidad.Id, hoy.AddDays(-60), hoy.AddDays(-1), usuarioId, ct);
        informe.Add("2. Restricción CADUCADA ayer → NO debe bloquear (SIM-0002 / SIM-P01)");

        var simPermanente = await CrearOUsarPersonaSimuladaAsync("SIM-0003", "Escenario 3 — Restricción permanente", 1, ct);
        await CrearRestriccionAsync(simPermanente.Id, capacidad.Id, hoy.AddDays(-100), null, usuarioId, ct);
        informe.Add("3. Restricción PERMANENTE (fecha_fin NULL) → bloquea siempre (SIM-0003 / SIM-P01)");

        var simFutura = await CrearOUsarPersonaSimuladaAsync("SIM-0004", "Escenario 4 — Restricción futura", 1, ct);
        await CrearRestriccionAsync(simFutura.Id, capacidad.Id, hoy.AddDays(1), null, usuarioId, ct);
        informe.Add("4. Restricción que empieza MAÑANA → todavía no vigente, no bloquea (SIM-0004 / SIM-P01)");

        // ── Escenarios 5-6: fatiga propia del puesto (A4/A14) ──
        informe.Add($"5. Puesto con fatiga 'sugerida' propia (horas_en_puesto=2) → {puestoRestriccion.Codigo}");
        var puestoCritico = await CrearOUsarPuestoAsync(2, "SIM-P02", "Puesto con umbral crítico válido (simulado)",
            horasEnPuesto: 5, umbralCriticoHoras: 8, ct: ct);
        informe.Add($"6. Puesto con umbral_critico_horas(8) > horas_en_puesto(5) → {puestoCritico.Codigo}");

        // ── Escenarios 7-8: recuperación generalizada (A12) ──
        var tipoGirarBotellas = await CrearOUsarTipoActividadAsync("Girar botellas", ct);
        var puestoGirarBotellas = await CrearOUsarPuestoAsync(1, "SIM-P03", "Girar botellas (simulado)",
            horasEnPuesto: 2, horasRecuperacion: 24, tipoActividadId: tipoGirarBotellas.Id, ct: ct);
        informe.Add($"7. 'Girar botellas' con horas_recuperacion=24 (A12) → {puestoGirarBotellas.Codigo}");

        var tipoLimpieza = await CrearOUsarTipoActividadAsync("Limpieza", ct);
        var puestoLimpieza = await CrearOUsarPuestoAsync(4, "SIM-P04", "Limpieza (simulado)",
            horasEnPuesto: 5, horasRecuperacion: 48, tipoActividadId: tipoLimpieza.Id, ct: ct);
        informe.Add($"8. 'Limpieza' con horas_recuperacion=48 (A12) → {puestoLimpieza.Codigo}");

        // ── Escenarios 9-10: sexo preferente, regla blanda (A13) ──
        var puestoSexo = await CrearOUsarPuestoAsync(6, "SIM-P05", "Puesto con sexo preferente (simulado)",
            horasEnPuesto: 3, sexoPreferente: "Masculino", ct: ct);
        var simSexoDistinto = await CrearOUsarPersonaSimuladaAsync("SIM-0005", "Escenario 9 — Sexo no coincide", 6, ct, sexo: "femenino");
        informe.Add($"9. Persona de sexo distinto al preferente del puesto (regla blanda, puede ceder) → SIM-0005 / {puestoSexo.Codigo}");

        var simSexoNulo = await CrearOUsarPersonaSimuladaAsync("SIM-0006", "Escenario 10 — Sexo no registrado", 6, ct, sexo: null);
        informe.Add($"10. Persona con sexo NULL → la regla no se evalúa, nunca se infiere (§7.3) → SIM-0006 / {puestoSexo.Codigo}");

        // ── Escenarios 11-13: Operador B/C simulados (G1) ──
        var lineasConB = new byte[] { 1, 2, 4, 8 }; // L6 deliberadamente se deja sin (escenario 13)
        foreach (var lineaId in lineasConB)
        {
            var relabelado = await RelabelarUnOperarioAsync(lineaId, "operador_b", ct);
            informe.Add(relabelado is null
                ? $"11. (sin efecto: no había operarios reales disponibles en L{lineaId} para re-etiquetar)"
                : $"11. Operador B disponible en L{lineaId} (simulado_categoria) → ficha {relabelado.Ficha}");
        }

        var relabeladoC = await RelabelarUnOperarioAsync(2, "operador_c", ct);
        informe.Add(relabeladoC is null
            ? "12. (sin efecto: no había operarios reales disponibles en L2 para re-etiquetar a Operador C)"
            : $"12. Operador C disponible en L2 (simulado_categoria) → ficha {relabeladoC.Ficha}");

        informe.Add("13. L6 deliberadamente sin ningún Operador B re-etiquetado → déficit (fuerza vacante crítica en E10)");

        // ── Escenarios 14-15: titular ausente con/sin suplente en su línea (C1) ──
        var titularL1 = await MarcarUnOperadorAAusenteAsync(1, usuarioId, ct);
        var puestoVacanteL1 = await CrearOUsarPuestoAsync(1, "SIM-P06", "Vacante crítica L1 — con suplente (simulado)",
            tipo: "fijo", titularId: titularL1?.Id, ct: ct);
        informe.Add(titularL1 is null
            ? "14. (sin efecto: no había Operador A real en L1 para marcar ausente)"
            : $"14. Titular ausente en L1 (ficha {titularL1.Ficha}) CON Operador B disponible en la misma línea (escenario 11) → {puestoVacanteL1.Codigo}");

        var titularL6 = await MarcarUnOperadorAAusenteAsync(6, usuarioId, ct);
        var puestoVacanteL6 = await CrearOUsarPuestoAsync(6, "SIM-P07", "Vacante crítica L6 — sin suplente en su línea (simulado)",
            tipo: "fijo", titularId: titularL6?.Id, ct: ct);
        informe.Add(titularL6 is null
            ? "15. (sin efecto: no había Operador A real en L6 para marcar ausente)"
            : $"15. Titular ausente en L6 (ficha {titularL6.Ficha}) SIN ningún Operador B en esa línea (escenario 13) → {puestoVacanteL6.Codigo}");

        // ── Escenario 16: piso de seguridad por línea (B5) ──
        await FijarPisoDeSeguridadAsync(lineaId: 2, minimo: 3, personasAsignadas: 3, ct); // exactamente en el mínimo
        await FijarPisoDeSeguridadAsync(lineaId: 4, minimo: 3, personasAsignadas: 4, ct); // una persona por encima
        informe.Add("16. L2 exactamente en su piso mínimo (3=3, inmune a extracción) y L4 una persona por encima (4>3) — 00 §B5");

        await db.SaveChangesAsync(ct);
        return string.Join(Environment.NewLine, informe);
    }

    private async Task<Puesto> CrearOUsarPuestoAsync(
        byte lineaId, string codigo, string nombre, CancellationToken ct,
        short? horasEnPuesto = null, short? umbralCriticoHoras = null, short? horasRecuperacion = null,
        string? sexoPreferente = null, short? tipoActividadId = null, string tipo = "rotativo", int? titularId = null)
    {
        var existente = await db.Puestos.SingleOrDefaultAsync(p => p.LineaId == lineaId && p.Codigo == codigo, ct);
        if (existente is not null) return existente;

        var puesto = new Puesto
        {
            LineaId = lineaId,
            Codigo = codigo,
            NombrePuesto = nombre,
            Tipo = tipo,
            HorasEnPuesto = horasEnPuesto,
            UmbralCriticoHoras = umbralCriticoHoras,
            HorasRecuperacion = horasRecuperacion,
            SexoPreferente = sexoPreferente,
            TipoActividadId = tipoActividadId,
            TitularId = titularId,
            CategoriaTitular = null, // 00 §G5
            OrigenDato = "simulado", // 07 §4.4 — sin esto la purga no lo ve
        };
        db.Puestos.Add(puesto);
        await db.SaveChangesAsync(ct);
        return puesto;
    }

    private async Task CrearPuestoCapacidadAsync(int puestoId, short capacidadId, CancellationToken ct)
    {
        var existe = await db.PuestosCapacidad.AnyAsync(pc => pc.PuestoId == puestoId && pc.CapacidadId == capacidadId, ct);
        if (existe) return;
        db.PuestosCapacidad.Add(new PuestoCapacidad { PuestoId = puestoId, CapacidadId = capacidadId });
        await db.SaveChangesAsync(ct);
    }

    private async Task<TipoActividad> CrearOUsarTipoActividadAsync(string nombre, CancellationToken ct)
    {
        var existente = await db.TiposActividad.SingleOrDefaultAsync(t => t.Nombre == nombre, ct);
        if (existente is not null) return existente;

        var tipo = new TipoActividad { Nombre = nombre };
        db.TiposActividad.Add(tipo);
        await db.SaveChangesAsync(ct);
        return tipo;
    }

    private async Task<Personal> CrearOUsarPersonaSimuladaAsync(
        string ficha, string nombre, byte lineaHabitual, CancellationToken ct, string? sexo = "masculino")
    {
        var existente = await db.Personas.SingleOrDefaultAsync(p => p.Ficha == ficha, ct);
        if (existente is not null) return existente;

        var persona = new Personal
        {
            Ficha = ficha,
            NombreCompleto = nombre,
            Categoria = "operario",
            Sexo = sexo,
            LineaHabitual = lineaHabitual,
            OrigenDato = "simulado",
        };
        db.Personas.Add(persona);
        await db.SaveChangesAsync(ct);
        return persona;
    }

    private async Task CrearRestriccionAsync(
        int personalId, short capacidadId, DateOnly inicio, DateOnly? fin, int usuarioId, CancellationToken ct)
    {
        var existe = await db.RestriccionesMedicas.AnyAsync(r =>
            r.PersonalId == personalId && r.CapacidadId == capacidadId && r.FechaInicio == inicio, ct);
        if (existe) return;

        db.RestriccionesMedicas.Add(new RestriccionMedica
        {
            PersonalId = personalId,
            CapacidadId = capacidadId,
            FechaInicio = inicio,
            FechaFin = fin,
            Fuente = "Semilla adversaria (07 §4.4) — no es un dictamen real",
            FechaDictamen = inicio,
            RegistradoPor = usuarioId,
            OrigenDato = "simulado",
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>00 §G1: re-etiqueta UN operario real de la línea dada — solo en esta semilla, nunca a producción.</summary>
    private async Task<Personal?> RelabelarUnOperarioAsync(byte lineaId, string categoria, CancellationToken ct)
    {
        var yaHay = await db.Personas.AnyAsync(p =>
            p.LineaHabitual == lineaId && p.Categoria == categoria && p.OrigenDato == "simulado_categoria", ct);
        if (yaHay) return await db.Personas.FirstAsync(p => p.LineaHabitual == lineaId && p.Categoria == categoria, ct);

        var candidato = await db.Personas
            .Where(p => p.LineaHabitual == lineaId && p.Categoria == "operario" && p.OrigenDato == "real")
            .OrderBy(p => p.Ficha)
            .FirstOrDefaultAsync(ct);
        if (candidato is null) return null;

        candidato.Categoria = categoria;
        candidato.OrigenDato = "simulado_categoria";
        await db.SaveChangesAsync(ct);
        return candidato;
    }

    /// <summary>Marca un Operador A real de la línea como ausente (situación + AusenciaJustificada), para el escenario de vacante crítica (C1).</summary>
    private async Task<Personal?> MarcarUnOperadorAAusenteAsync(byte lineaId, int usuarioId, CancellationToken ct)
    {
        var candidato = await db.Personas
            .Where(p => p.LineaHabitual == lineaId && p.Categoria == "operador_a")
            .OrderBy(p => p.Ficha)
            .FirstOrDefaultAsync(ct);
        if (candidato is null) return null;

        candidato.Situacion = "ausente_justificado";
        var yaTieneAusencia = await db.AusenciasJustificadas.AnyAsync(a => a.PersonalId == candidato.Id && a.FechaFin == null, ct);
        if (!yaTieneAusencia)
        {
            db.AusenciasJustificadas.Add(new AusenciaJustificada
            {
                PersonalId = candidato.Id,
                Tipo = "otro",
                FechaInicio = DateOnly.FromDateTime(DateTime.UtcNow),
                FechaFin = null,
                RegistradoPor = usuarioId,
                OrigenDato = "simulado", // 07 §4.4 — la ausencia es fabricada, la persona no
            });
        }
        await db.SaveChangesAsync(ct);
        return candidato;
    }

    /// <summary>00 §B5: fija el piso de la línea y cuenta exactamente esa cantidad de personas "asignadas" (aprox. sin Asignacion todavía — E5).</summary>
    private async Task FijarPisoDeSeguridadAsync(byte lineaId, short minimo, int personasAsignadas, CancellationToken ct)
    {
        var linea = await db.Lineas.SingleAsync(l => l.Id == lineaId, ct);
        linea.MinimoOperarios = minimo;

        var candidatos = await db.Personas
            .Where(p => p.LineaHabitual == lineaId && p.Categoria == "operario" && p.Situacion != "asignado")
            .OrderBy(p => p.Ficha)
            .Take(personasAsignadas)
            .ToListAsync(ct);

        foreach (var persona in candidatos)
        {
            persona.Situacion = "asignado";
            persona.LineaFisicaActual = lineaId;
        }

        await db.SaveChangesAsync(ct);
    }
}
