# SmartAssign — Estado de ejecución

**Se lee al empezar cada sesión. Se actualiza al terminar cada UT.**
Última actualización: 2026-08-09 · UTs completadas: **0 / 95**

> **Protocolo de sesión:**
> 1. Leer este fichero → identificar la siguiente UT sin marcar.
> 2. Leer **solo** lo que declara su columna `LEE`. Nunca la documentación entera.
> 3. Ejecutar. Si falta un dato de negocio → **detenerse y preguntar** *(regla R2)*.
> 4. Verificar en verde. Sin verde, la UT sigue abierta.
> 5. Marcar aquí + un commit citando el ID.

**Leyenda:** `[ ]` pendiente · `[~]` en curso · `[x]` verificada · `[!]` bloqueada

---

## E0 · Entorno *(0/3)*

- [ ] **E0.1** Fijar `JAVA_HOME` al JBR de Android Studio; comprobar Gradle y LocalDB
  - LEE: `07 §3`
  - VERIFICA: `gradle -v` responde · `sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "SELECT 1"` devuelve 1
- [ ] **E0.2** Esqueleto de solución backend
  - LEE: `05 §5`, `05 §4.1`
  - VERIFICA: `dotnet build` sin errores · `dotnet test` corre (0 pruebas)
- [ ] **E0.3** Esqueleto Android + CI local
  - LEE: `05 §4.2`, `05 §1.5`
  - VERIFICA: `gradlew assembleDebug` genera APK

## E1 · Cimientos y semilla estructural *(0/6)* → F0

- [ ] **E1.1** Migración 001: líneas, puestos, catálogos base
  - LEE: `04 §2.1`, `04 §2.5`, `04 §2.6`
  - VERIFICA: migración aplica y revierte contra LocalDB
- [ ] **E1.2** Semilla estructural: 10 líneas, L8 Bolsón, prioridad base
  - LEE: `04 §2.1`, `04 §2.2`, `§3.2`, `§3.3`
  - VERIFICA: 10 líneas · exactamente una con `es_bolson=1` · orden de prioridad correcto
- [ ] **E1.3** Semilla `ProximidadLinea` — **grafo dirigido, corrección A1**
  - LEE: `00 §A1`, `00 §A2`, `00 §A3`, `04 §2.3`
  - VERIFICA: 90 filas · L8 sin filas como origen · ninguna fila con origen = destino
- [ ] **E1.4** Prueba dedicada de la fila de L10
  - LEE: `00 §A1`
  - VERIFICA: L10 = `L9, L3, L6, L7, L4, L2, L1, L5, L8` — celda por celda
- [ ] **E1.5** Semilla de capacidades físicas y catálogos de excepción/rechazo/paro
  - LEE: `04 §2.7`, `04 §5.4`, `04 §4.3`
  - VERIFICA: conteos esperados
- [ ] **E1.6** Pipeline CI: build, test, arquitectura, secretos
  - LEE: `06 §4.3`
  - VERIFICA: pipeline en verde en local

## E2 · Identidad y aislamiento *(0/6)* → F1 · `[BLOQUEANTE]`

- [ ] **E2.1** `Usuario`, `SesionDispositivo`, `DispositivoPush`
  - LEE: `04 §6.1`, `04 §10`
  - VERIFICA: migración aplica y revierte
- [ ] **E2.2** JWT + refresh + PIN — **sin `linea_id` en el token**
  - LEE: `00 §D6`, `04 §6.4`, `§2.3`
  - VERIFICA: prueba que inspecciona los claims y falla si aparece `linea_id`
- [ ] **E2.3** Filtro de rol + filtro de alcance por línea
  - LEE: `04 §6.2`, `§2.2`
  - VERIFICA: supervisor de L2 rechazado en todo endpoint de L4
- [ ] **E2.4** RLS en SQL Server como tercera capa
  - LEE: `04 §6.3`
  - VERIFICA: con el filtro de aplicación desactivado, la consulta sigue bloqueada
- [ ] **E2.5** `Auditoria` + `sp_RegistrarAuditoria`, **incluidos los rechazos**
  - LEE: `04 §8`, `§12.7`
  - VERIFICA: una operación y un rechazo dejan fila
- [ ] **E2.6** Suite de aislamiento completa
  - LEE: `05 §6.2`
  - VERIFICA: suite en verde

> **→ PC-1** · Validación humana: dos supervisores en dos teléfonos, ninguno ve la línea del otro.

## E3 · Personal y puestos *(0/6)* → F2

- [ ] **E3.1** `Puesto` con umbrales propios nulables y `titular_id` de doble semántica
  - LEE: `04 §2.6`, `00 §A4`, `00 §C12`
  - VERIFICA: `CK_Puesto_umbrales` rechaza crítico ≤ sugerido
- [ ] **E3.2** `PuestoCapacidad` y `TipoActividad` con bandera de 24 h
  - LEE: `04 §2.4`, `04 §2.7`, `00 §A4`
  - VERIFICA: solo "Girar botellas" con la bandera activa
- [ ] **E3.3** `Personal` — `perfil` nulable, `linea_habitual`
  - LEE: `04 §3.1`, `§7.3`, `00 §C3`
  - VERIFICA: prueba de que `perfil` NULL significa *no evaluar*
- [ ] **E3.4** `RestriccionMedica` con vigencia y sin borrado
  - LEE: `04 §3.2`, `00 §C14`, `§7.2`
  - VERIFICA: `DELETE` denegado a la cuenta de aplicación
- [ ] **E3.5** **Semilla simulada adversaria** — los 16 escenarios
  - LEE: `07 §4.1`, `07 §4.2`
  - VERIFICA: una prueba por escenario confirma que existe en la semilla
- [ ] **E3.6** Importador de datos reales con rechazo por lote
  - LEE: `07 §4.3`, `04 §3`
  - VERIFICA: fila inválida → se rechaza el lote entero, con informe

## E4 · Motor de validación *(0/8)* → F3 · `[BLOQUEANTE]`

- [ ] **E4.1** `fn_TieneRestriccionBloqueante` — solo vigentes
  - LEE: `§7.2`, `00 §C14`, `04 §7.2`
  - VERIFICA: vigente bloquea · caducada no · permanente siempre
- [ ] **E4.2** `fn_CategoriaCompatible` — matriz §4.2 completa
  - LEE: `§4.2`, `00 §A7`, `00 §A7b`
  - VERIFICA: cada casilla de la matriz, incluidas las prohibidas
- [ ] **E4.3** `fn_PerfilIncompatible` — regla blanda
  - LEE: `§7.3`, `00 §B12`
  - VERIFICA: `perfil` NULL → la regla **no** se aplica
- [ ] **E4.4** `fn_ViolaNoRepeticion24h` — solo la actividad marcada
  - LEE: `§7.4`, `00 §A4`, `00 §B6`
  - VERIFICA: otro puesto no bloquea · 3 jornadas de descanso **sí** bloquea
- [ ] **E4.5** `fn_VentanaArranqueBloquea`
  - LEE: `§8.4`, `04 §4.1`
  - VERIFICA: bloquea a quien no está físicamente en la línea
- [ ] **E4.6** `sp_ValidarAsignacion` — los 7 pasos en orden
  - LEE: `§7.1`, `00 §B12`, `04 §7.2`
  - VERIFICA: el orden exacto · el primer rechazo detiene · ningún parámetro salta el paso 4
- [ ] **E4.7** `DENY` sobre tablas críticas
  - LEE: `04 §7.5`
  - VERIFICA: `INSERT` directo en `Asignacion` con la cuenta de app **falla**
- [ ] **E4.8** Suite de reglas de seguridad — médicas × 8 caminos
  - LEE: `05 §6.2`
  - VERIFICA: los 8 caminos deniegan, cada uno con su mensaje

> **→ PC-2** · Validación humana: la restricción médica bloquea por los ocho caminos.

## E5 · Jornada, prioridad y barrido *(0/7)* → F4a

- [ ] **E5.1** `Turno` y día de operación · hora del servidor — LEE: `04 §4.1`, `00 §C6`
- [ ] **E5.2** `JornadaLinea` y `PrioridadLinea` versionada — LEE: `04 §2.2`, `00 §B8`
- [ ] **E5.3** `SKU`, `PuestoSKU`, cómputo de *fuera de operación* — LEE: `04 §2.5`, `§5.3`
- [ ] **E5.4** Planificación + rechazo si falta supervisor — LEE: `§8.1`, `02 §3.1`
- [ ] **E5.5** `sp_BarridoPuestosFijos` — por prioridad, **solo `tipo='fijo'`** — LEE: `§8.3`, `00 §C12`
- [ ] **E5.6** `titular_original_id`, vacante crítica, micro-copia §12.5 — LEE: `§8.3`, `§12.5`
- [ ] **E5.7** Ventana de arranque por jornada-línea — LEE: `§8.4`, `02 §4.1`

## E6 · App base y llenado *(0/8)* → F4b

- [ ] **E6.1** Sistema de diseño en Compose — LEE: `03 §2`, `00 §A11`
- [ ] **E6.2** Los cuatro estados de pantalla — LEE: `03 §3.11`, `§12.4`
- [ ] **E6.3** Login, PIN, alta de dispositivo por QR — LEE: `02 §1.0`, `02 §1.1`, `00 §F3`
- [ ] **E6.4** Malla de línea — LEE: `03 §3.1`, `03 §4.3`
- [ ] **E6.5** Escáner QR con ML Kit en dispositivo — LEE: `00 §E1`, `§12.1`
- [ ] **E6.6** Modal de confirmación de identidad — LEE: `03 §3.3`, `§12.2`
- [ ] **E6.7** `sp_SugerirPuesto` — escalera de 4 niveles — LEE: `§8.5`
- [ ] **E6.8** Concurrencia: bloqueo determinista + idempotencia — LEE: `04 §7.3`, `00 §B1`

> **→ PC-3** · **El punto de control más importante.** Un supervisor llena su línea desde un teléfono real.

## E7 · Fatiga *(0/4)* → F5

- [ ] **E7.1** Reloj desde `Asignacion.inicio` con umbral propio — LEE: `§9.1`, `00 §A4`
- [ ] **E7.2** Exceso relativo en % — LEE: `00 §A4`, `00 §B3`
- [ ] **E7.3** Tres niveles · fijos no acumulan · fatiga del puesto — LEE: `§9.1`, `§5.1`, `00 §A7`
- [ ] **E7.4** Factor de doble turno (default 1.0) + barra continua — LEE: `00 §B7`, `§11.5`

## E8 · Movimiento entre líneas *(0/6)* → F6

- [ ] **E8.1** Despacho con `hora_salida` — LEE: `Parte X`, `§12.7`, `04 §5.2`
- [ ] **E8.2** Tránsito inmune — LEE: `§6.1`, `04 §5.2`
- [ ] **E8.3** Recepción individual con `hora_llegada` — LEE: `00 §C8`, `03 §4.5`
- [ ] **E8.4** Rechazo con motivo obligatorio — LEE: `00 §C10`
- [ ] **E8.5** Reserva de puesto sin convergencia — LEE: `00 §B4`, `04 §5.2`
- [ ] **E8.6** `sp_CaducarTransitos` — alerta sin mover a nadie — LEE: `00 §B11`

## E9 · Motor de relevos *(0/9)* → F7

- [ ] **E9.1** `SolicitudRelevo` · el puesto no se libera al avisar — LEE: `§9.4 p1`, `04 §5.3`
- [ ] **E9.2** Orden de cola: crítico → exceso relativo → FIFO — LEE: `00 §B3`
- [ ] **E9.3** Aviso a todos los supervisores **sin identidad** — LEE: `00 §D2`, `§2.2`
- [ ] **E9.4** `vw_SolicitudRelevo_L8` — proyección mínima — LEE: `00 §D1`, `04 §6.3`
- [ ] **E9.5** `sp_ProponerRelevista` — ranking exacto — LEE: `00 §B2`
- [ ] **E9.6** Aceptar/rechazar + descartados con caducidad — LEE: `§9.4 p3`, `00 §B10`
- [ ] **E9.7** `sp_SugerirDestinoRelevado` — línea → proximidad → L8 — LEE: `00 §B4`, `00 §A1`, `00 §A9`
- [ ] **E9.8** Prueba del ejemplo normativo del §9.4 — LEE: `§9.4 ejemplo`, `00 §A8`
- [ ] **E9.9** Prueba de arquitectura: Relevos no referencia prioridad — LEE: `00 §A9`, `05 §4.1`

> **→ PC-4** · El ejemplo del §9.4 completo, con relevo en cadena.

## E10 · Extracción inversa y vacante crítica *(0/6)* → F8

- [ ] **E10.1** Orden derivado invirtiendo la prioridad — LEE: `00 §A5`, `§9.6`
- [ ] **E10.2** Solo con la L8 completamente vacía — LEE: `§9.6`
- [ ] **E10.3** Piso por línea · inmunidad — LEE: `00 §B5`
- [ ] **E10.4** Escalera C15 N1→N4 + guarda anti-dominó — LEE: `00 §C15`
- [ ] **E10.5** `JustificacionExcepcion` en toda excepción — LEE: `00 §A6`, `04 §5.4`
- [ ] **E10.6** Titular reincorporado + salida de retiro temporal — LEE: `00 §C1`, `00 §C2`

> ⚠ **Bloqueada hasta H7:** cerrar `A5b` (si L4 puede ser donante) y `A7-orig` (liderazgo y matriz de categoría).

## E11 · Contingencias y estadística *(0/8)* → F9

- [ ] **E11.1** Paro: dos niveles + descripción obligatoria — LEE: `§11.1`, `04 §4.3`
- [ ] **E11.2** Fijos ocupados · rotativos liberados con tránsito individual — LEE: `§11.1`, `00 §C8`
- [ ] **E11.3** Cronómetro persistente en todas las pantallas — LEE: `§11.1`, `03 §3.8`
- [ ] **E11.4** Relevista en tránsito hacia línea en paro — LEE: `00 §C9`
- [ ] **E11.5** `Lote` y cambio de SKU — LEE: `00 §C5`, `§11.2`
- [ ] **E11.6** Desperdicio + producción + justificación sobre umbral — LEE: `§11.3`, `00 §C4`
- [ ] **E11.7** Eficiencia calculada en el servidor — LEE: `§11.4`, `00 §C4`
- [ ] **E11.8** Todo registro empuja a los dos paneles — LEE: `00 §C4`

## E12 · Tiempo real y notificaciones *(0/6)* → F10

- [ ] **E12.1** Hub SignalR con grupos asignados por el servidor — LEE: `05 §2.4`, `§2.2`
- [ ] **E12.2** Catálogo de eventos — LEE: `05 §2.4`
- [ ] **E12.3** Bandeja de salida transaccional — LEE: `05 §4.1`
- [ ] **E12.4** FCM campana vacía + descarga del contenido real — LEE: `00 §D5`, `05 §2.5`
- [ ] **E12.5** Prueba que falla si la carga útil lleva negocio — LEE: `00 §D5`, `§12.1`
- [ ] **E12.6** Acuse, escalado y *"supervisor no localizable"* — LEE: `00 §D5`, `04 §10`

## E13 · Modo sin conexión *(0/5)* → F11

- [ ] **E13.1** Room + SQLCipher con clave en Keystore — LEE: `00 §D3`
- [ ] **E13.2** Caché acotada a su línea — LEE: `00 §D3`
- [ ] **E13.3** Bloqueo defensivo · **no se encola nada** — LEE: `§12.1`, `05 §4.3`
- [ ] **E13.4** Sello de frescura + degradación visual — LEE: `00 §D4`, `03 §3.7`
- [ ] **E13.5** Detección por latido — LEE: `05 §4.3`

> **→ PC-5** · Se corta la red y nada queda encolado.

## E14 · Cierre, histórico y endurecimiento *(0/7)* → F12

- [ ] **E14.1** Cierre de turno con lista exacta de bloqueos — LEE: `00 §C13`, `02 §4.10`
- [ ] **E14.2** `UltimaTareaJornada` + cierre forzado con justificación — LEE: `00 §B6`, `00 §A6`
- [ ] **E14.3** Histórico y auditoría consultable — LEE: `§2.1.11`, `§12.7`
- [ ] **E14.4** Rendimiento contra presupuestos — LEE: `05 §3.4`
- [ ] **E14.5** Accesibilidad: 48 dp, AAA, escala de grises — LEE: `03 §5`, `00 §A11`
- [ ] **E14.6** Distribución del APK + verificación de versión — LEE: `00 §F3`, `04 §10.1`
- [ ] **E14.7** Carga de datos reales + purga de lo simulado — LEE: `07 §4.3`

> **→ PC-6** · Piloto: un turno real completo, sin papel.

---

## Bloqueos activos

| ID | Qué falta | Bloquea |
|---|---|---|
| **H7** | Cerrar `A5b` y `A7-orig` | E10 |
| **H4** | Gafetes impresos con QR | Pruebas de campo desde E6 |
| **H6** | Vocabulario real de capacidades físicas | Producción, no construcción |
| **H2** | Teléfonos físicos | PC-1 en adelante |

## Registro de sesiones

| Fecha | UTs completadas | Notas |
|---|---|---|
| — | — | Sin sesiones de construcción todavía |
