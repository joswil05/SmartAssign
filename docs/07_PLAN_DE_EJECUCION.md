# SmartAssign — Plan de Ejecución

**Sprint a sprint: qué se construye, con qué se demuestra, y qué hace falta para empezar.**
Versión 1.0 · 2026-08-09

> **Diferencia con el [Roadmap](06_ROADMAP.md).** El roadmap dice **en qué orden y por qué** — el grafo de dependencias, las 13 fases y sus criterios de salida. Este documento dice **qué se hace exactamente cada sprint**, quién entrega qué, cómo se demuestra y qué tiene que aportar el cliente para no bloquear el avance.
>
> **Todo lo que aquí se planifica está declarado en los documentos anteriores.** Si una tarea de este plan no puede citar una sección de [01](01_PRD.md)–[06](06_ROADMAP.md) o una decisión del [registro](00_DECISIONES.md), no debería estar en el plan.

---

# 1 · Punto de partida

## 1.1 Qué existe hoy

| Existe | Estado |
|---|---|
| Especificación funcional v3.3 + anexo | En [`docs/fuentes/`](fuentes/) |
| Registro de decisiones — 53 cerradas | [00_DECISIONES.md](00_DECISIONES.md) |
| PRD, Flujos, UI/UX, Esquema, TRD, Roadmap | [01](01_PRD.md)–[06](06_ROADMAP.md) |
| Repositorio git | Inicializado, 2 commits |
| **Código** | **Ninguno.** Este plan empieza en cero |

## 1.2 Supuestos del plan — confirmar antes de fijar calendario

> ⚠ **El plan se expresa en sprints, no en fechas.** La conversión a calendario depende de dos datos que no tengo:

| Supuesto | Valor asumido | Si es distinto |
|---|---|---|
| **Equipo** | Un desarrollador, con asistencia de IA según el encadenamiento de prompts de [06 §3](06_ROADMAP.md) | Con dos personas, los bloques B y D se paralelizan parcialmente y bajan ~4 sprints |
| **Duración del sprint** | 1 semana | A 2 semanas, todo se duplica en calendario |
| **Dedicación** | Tiempo completo | A media dedicación, cuenta el doble de sprints |

Con los supuestos de arriba: **21 sprints ≈ 5 meses** hasta el piloto en planta.

---

# 2 · Los cinco bloques entregables

Las 13 fases del roadmap son la unidad técnica. Pero 13 hitos son demasiados para seguir el avance, y varios no se pueden enseñar a nadie. Por eso se agrupan en **5 bloques, cada uno terminado en una demostración que el cliente puede juzgar sin saber programar**.

```
BLOQUE A · EL ESQUELETO SEGURO          F0 + F1        2 sprints
   demo: dos supervisores entran y ninguno ve la línea del otro
        ↓
BLOQUE B · LA PLANTA EN DATOS           F2 + F3        3 sprints
   demo: la restricción médica bloquea por los 8 caminos posibles
        ↓
BLOQUE C · UN TURNO QUE ARRANCA         F4             3 sprints
   demo: arranca un turno y una línea se llena desde el teléfono
        ↓                                    ← primer producto reconocible
BLOQUE D · EL SISTEMA QUE ROTA          F5+F6+F7+F8    6 sprints
   demo: el ejemplo del §9.4 completo, con relevo en cadena
        ↓                                    ← el corazón del producto
BLOQUE E · EL TURNO COMPLETO            F9+F10+F11     4 sprints
   demo: paro, estadística en dos paneles, notificación, sin red
        ↓
BLOQUE F · CIERRE Y PILOTO              F12            3 sprints
   demo: un turno real completo, sin papel
```

> **Por qué el bloque C es el punto de inflexión.** Hasta ahí, todo lo construido es invisible: base de datos, autenticación, reglas. Al terminar C, el cliente ve por primera vez **un supervisor colocando gente en su línea desde un teléfono**. Es el momento de validar que el producto se entiende antes de invertir los 6 sprints del bloque D.

---

# 3 · Sprint 0 · Preparación

**No se escribe código de aplicación.** Se retiran los obstáculos que bloquearían más adelante.

## 3.1 Infraestructura

| # | Tarea | Resultado |
|---|---|---|
| 0.1 | Provisionar servidor Windows + SQL Server 2019+ | Cadena de conexión funcionando ⚠ `E3` |
| 0.2 | Crear base de desarrollo y de preproducción | Dos entornos aislados |
| 0.3 | **Conseguir 3 teléfonos Android de prueba** | Ver nota abajo ⚠ `E4` |
| 0.4 | Crear proyecto Firebase solo para envío de FCM | Credenciales de servidor *(D5)* |
| 0.5 | Verificar salida a internet desde los teléfonos | Ya confirmado *(E5)* — solo comprobar en sitio |

> **Tres teléfonos, no uno.** Con un solo dispositivo es **imposible probar** las dos reglas más importantes del sistema: el aislamiento entre supervisores *(§2.2)* necesita dos sesiones simultáneas de líneas distintas, y el desempate de concurrencia *(§7.5, B1)* necesita dos supervisores capturando a la misma persona a la vez. El tercero es para el Coordinador.

## 3.2 Arranque de la dependencia física

| # | Tarea | Por qué ahora |
|---|---|---|
| 0.6 | **Iniciar el diseño e impresión de los gafetes con QR** | Es lo único del camino crítico que **no depende del desarrollo**. Especificación en [00 §E1](00_DECISIONES.md): QR ≥ 25 mm, corrección M o superior, solo el número de ficha, y el número también impreso en claro al lado |

> **Se arranca en el sprint 0 aunque no se necesite hasta el sprint 8.** Diseñar la etiqueta, imprimir y distribuir a ~160 personas tiene un plazo que no controla el equipo de desarrollo. Si se deja para cuando haga falta, bloquea.

## 3.3 Petición formal de datos maestros

Se entrega al cliente la plantilla de los datos que tendrá que aportar (§4 de este documento) y se acuerdan fechas.

**Entregable del sprint 0:** entornos listos, teléfonos disponibles, impresión de gafetes iniciada, plantillas de datos entregadas.

---

# 4 · Lo que tiene que aportar el cliente

**Es la parte del plan que más riesgo de retraso tiene**, porque no depende del desarrollo y suele subestimarse.

## 4.1 Calendario de entregas

| # | Dato | Volumen | Necesario antes de | Bloquea |
|---|---|---|---|---|
| **D1** | **Vocabulario de capacidades físicas** | ~10–20 códigos | **Sprint 3** | Toda la regla médica |
| D2 | Catálogo de puestos por línea | ~300 | Sprint 3 | Modelo de puestos |
| D3 | Padrón de personal | ~160 | Sprint 4 | Todo lo que asigna |
| D4 | Restricciones médicas vigentes | Variable | Sprint 4 | Regla dura §7.2 |
| D5 | Catálogo de SKU + ritmo teórico | Decenas | Sprint 6 | Eficiencia, puestos activos |
| D6 | Qué puestos requiere cada SKU | ~300 × SKU | Sprint 6 | "Fuera de operación" |
| D7 | Horarios de turno | 2–3 | Sprint 6 | Jornadas |
| D8 | **Gafetes impresos** | ~160 | **Sprint 8** | Pruebas de campo |
| D9 | Piso de seguridad por línea | 10 valores | Sprint 13 | Extracción inversa |
| D10 | Categorías y causas de paro | ~4 × N | Sprint 15 | Registro de paros |
| D11 | Umbral de desperdicio y tramos de eficiencia | 3 valores | Sprint 16 | Estadística |

## 4.2 D1 merece atención aparte

> **Todo el mecanismo de la regla médica depende de un vocabulario compartido** *(§7.2, [04 §2.7](04_ESQUEMA_BACKEND.md))*: la persona tiene *capacidades prohibidas*, el puesto *exige capacidades*, y el sistema compara. **Si no coinciden literalmente, no hay comparación posible.**

El problema práctico: Enfermería no escribe códigos, escribe frases. *"No puede cargar peso"*, *"evitar estar mucho tiempo parada"*, *"no movimientos repetitivos con la mano derecha"*.

Alguien tiene que convertir eso en un catálogo cerrado — por ejemplo `levantar_carga`, `bipedestacion_prolongada`, `movimiento_repetitivo_mano` — y **mapear cada restricción existente a ese catálogo**. No es trabajo de programación: es trabajo de negocio con Enfermería.

**Sin D1 no se puede construir el bloque B.** Es la dependencia de datos más silenciosa y la que más veces se descubre tarde.

## 4.3 Umbrales de fatiga: no se piden ahora

`A4` estableció que se calibran **con datos reales de operación**. El sistema se construye con los campos vacíos y un valor de planta por defecto; los valores definitivos salen del piloto *(bloque F)*, usando los tiempos de traslado que §12.7 obliga a registrar. **No se pide al cliente que los invente antes de tener datos.**

---

# 5 · Bloque A · El esqueleto seguro

**Fases F0 + F1 · 2 sprints**

## Sprint 1 — Cimientos

| # | Tarea | Referencia |
|---|---|---|
| 1.1 | Estructura de solución backend y proyecto Android | [05 §5](05_TRD.md) |
| 1.2 | Migración 001: catálogos y estructura de planta | [04 §2](04_ESQUEMA_BACKEND.md) |
| 1.3 | **Semilla: 10 líneas, `es_bolson` en L8, prioridad base** | §3.2, §3.3 |
| 1.4 | **Semilla: `ProximidadLinea` completa con la corrección A1** | **A1, A2, A3** |
| 1.5 | Prueba que verifica la fila de L10 celda por celda | A1 |
| 1.6 | CI: compilar, pruebas, análisis estático, detección de secretos | [06 §4.3](06_ROADMAP.md) |
| 1.7 | Publicación como ejecutable único, servicio de Windows | F2 |

> **1.4 y 1.5 parecen triviales y no lo son.** La tabla de proximidad es un dato de negocio corregido a mano. Si entra mal, el motor de relevos del sprint 12 funcionará **perfectamente enviando gente al sitio equivocado**, y el fallo será invisible hasta que alguien camine de más. Se verifica aquí, no allí.

**Demostrable:** se clona el repositorio, se ejecuta un comando y hay base y API corriendo. CI en verde.

## Sprint 2 — Identidad y aislamiento `[BLOQUEANTE]`

| # | Tarea | Referencia |
|---|---|---|
| 2.1 | `Usuario`, `SesionDispositivo`, migración | [04 §6.1](04_ESQUEMA_BACKEND.md) |
| 2.2 | Login, JWT + refresh, PIN de reentrada | D6 |
| 2.3 | **El token NO lleva `linea_id`** — se resuelve en vivo | **§2.3** |
| 2.4 | Filtro de rol + filtro de alcance por línea | [04 §6.2](04_ESQUEMA_BACKEND.md) |
| 2.5 | **RLS en SQL Server como tercera capa** | [04 §6.3](04_ESQUEMA_BACKEND.md) |
| 2.6 | `Auditoria` + `sp_RegistrarAuditoria`, incluidos rechazos | §12.7 |
| 2.7 | Pantalla de login y desbloqueo por PIN | [02 §1.1](02_FLUJOS_DE_APP.md) |
| 2.8 | Pantalla terminal *"No tienes línea asignada"* | §2.3 |
| 2.9 | **Suite de aislamiento** | [05 §6.2](05_TRD.md) |

### 🎬 Demostración del bloque A

> Dos teléfonos, dos supervisores de líneas distintas. Se intenta, desde el segundo, acceder a cualquier dato del primero — por la interfaz y por llamada directa a la API. **Falla siempre.** Se abre la traza de auditoría y ahí está cada intento registrado.

**Criterio de salida:** la RLS bloquea aunque se desactive el filtro de aplicación. Si esto no se cumple, no se avanza — todo lo demás se construye encima.

---

# 6 · Bloque B · La planta en datos

**Fases F2 + F3 · 3 sprints**

## Sprint 3 — Vocabulario y puestos

| # | Tarea | Referencia |
|---|---|---|
| 3.1 | `CapacidadFisica` + carga del vocabulario **D1** | §7.2 |
| 3.2 | `Linea`, `Puesto` con **umbrales propios** nulables | **A4** |
| 3.3 | `PuestoCapacidad` — qué exige cada puesto | §7.2 |
| 3.4 | `TipoActividad` con `aplica_no_repeticion_24h` | **A4** |
| 3.5 | `titular_id` en ambos tipos, con semántica distinta | **C12** |
| 3.6 | Carga de **D2** (catálogo de puestos) | — |

## Sprint 4 — Padrón y datos médicos

| # | Tarea | Referencia |
|---|---|---|
| 4.1 | `Personal` con `linea_habitual`, `perfil` **nulable** | §7.3, C3 |
| 4.2 | `RestriccionMedica` **con vigencia, sin borrado** | **C14** |
| 4.3 | `AusenciaJustificada` | §6 |
| 4.4 | Pantallas de padrón del Coordinador, **búsqueda primero** | [03 §4.6](03_UIUX_BRIEF.md), F4 |
| 4.5 | Carga de **D3** y **D4** | — |

> **4.1 tiene una trampa conocida.** `perfil` nulable significa **"no evaluar"**, nunca "no cumple" — §7.3 es explícito: *"Si el dato de la persona no está registrado, la regla no se aplica. Nunca se infiere ni se deduce."*

## Sprint 5 — Motor de validación `[BLOQUEANTE]`

| # | Tarea | Referencia |
|---|---|---|
| 5.1 | `fn_TieneRestriccionBloqueante` — solo vigentes | §7.2, C14 |
| 5.2 | `fn_CategoriaCompatible` — matriz §4.2 casilla por casilla | §4.2, A7 |
| 5.3 | `fn_PerfilIncompatible` — cede, y NULL no aplica | §7.3 |
| 5.4 | `fn_ViolaNoRepeticion24h` — **solo la actividad marcada** | **A4, B6** |
| 5.5 | `fn_VentanaArranqueBloquea` | §8.4 |
| 5.6 | **`sp_ValidarAsignacion`: los 7 pasos, en orden** | **§7.1** |
| 5.7 | Catálogo de mensajes **literal** de [02 §5.4](02_FLUJOS_DE_APP.md) | §1.3, §12.4 |
| 5.8 | `DENY INSERT/UPDATE/DELETE` a la cuenta de aplicación | [04 §7.5](04_ESQUEMA_BACKEND.md) |
| 5.9 | **Suite de reglas de seguridad** | [05 §6.2](05_TRD.md) |

### 🎬 Demostración del bloque B

> Se toma una persona con restricción médica y un puesto que exige esa capacidad. Se intenta asignarla **por los ocho caminos del sistema** — incluido el del Coordinador con su formulario de excepción. **Falla en los ocho**, y cada vez dice exactamente por qué, en lenguaje de planta.
>
> Después se intenta insertar la asignación **directamente en la base con las credenciales de la aplicación**. También falla.

**Criterio de salida:** cobertura del 100 % en validación. Ningún parámetro del procedimiento puede saltar el paso 4.

---

# 7 · Bloque C · Un turno que arranca

**Fase F4 · 3 sprints**

## Sprint 6 — Turnos, jornada y planificación

| # | Tarea | Referencia |
|---|---|---|
| 6.1 | `Turno`, **día de operación**, hora siempre del servidor | **C6** |
| 6.2 | `JornadaLinea`, `PrioridadLinea` versionada | §8.1, B8 |
| 6.3 | `SKU`, `PuestoSKU` → cómputo de *"fuera de operación"* | §5.3, §11.2 |
| 6.4 | Planificación: líneas, SKU, supervisores, cobertura prevista | §8.1 |
| 6.5 | Rechazo si hay línea activa sin supervisor | §8.1 |
| 6.6 | Carga de **D5**, **D6**, **D7** | — |

## Sprint 7 — Barrido automático de puestos fijos

| # | Tarea | Referencia |
|---|---|---|
| 7.1 | `sp_BarridoPuestosFijos` recorriendo **por prioridad** | §8.3 |
| 7.2 | **Filtro `tipo='fijo'`** — los rotativos empiezan vacíos | §5.2, C12 |
| 7.3 | Conservar `titular_original_id` al usar suplente | §8.3 |
| 7.4 | Vacante crítica destacada sobre las normales | §5.3 |
| 7.5 | Micro-copia del §12.5, **literal** | §12.5 |
| 7.6 | Gatillo de arranque + ventana de arranque por jornada-línea | §8.4 |

## Sprint 8 — Llenado desde el teléfono

| # | Tarea | Referencia |
|---|---|---|
| 8.1 | Malla de línea, una columna, agrupada fijos → rotativos | [03 §4.3](03_UIUX_BRIEF.md) |
| 8.2 | Los cuatro estados de pantalla, **cargando ≠ vacío** | §12.4 |
| 8.3 | Escáner QR con CameraX + ML Kit **en dispositivo** | E1, §12.1 |
| 8.4 | **Modal de confirmación de identidad** con médicas explícitas | §12.2 |
| 8.5 | Búsqueda manual: solo disponibles, los de su línea primero | §12.2 |
| 8.6 | `sp_SugerirPuesto` — escalera de 4 niveles | §8.5 |
| 8.7 | Concurrencia: `UPDLOCK` + índices únicos + idempotencia | §7.5, B1 |
| 8.8 | Ventana de arranque bloqueando en la interfaz | §8.4 |
| 8.9 | **Alta de dispositivo por QR** | F3 |

### 🎬 Demostración del bloque C — el punto de inflexión

> El Coordinador planifica el día desde su teléfono y arranca el turno. El barrido cubre los puestos fijos por prioridad y deja los rotativos vacíos. Un supervisor recoge a su gente, escanea gafetes y llena su línea. Durante la ventana de arranque, intenta registrar a alguien de otra línea y el sistema se lo explica.
>
> **Dos supervisores escanean al mismo operario a la vez.** Uno gana; el otro recibe: *"[Nombre] acaba de ser registrado en L4 · Puesto 3 por otro supervisor."*

> **Aquí se para y se valida con el cliente antes de seguir.** Es la primera vez que el producto se puede juzgar de verdad, y quedan 6 sprints del bloque D por delante. Si algo de fondo no se entiende, es mucho más barato descubrirlo ahora.

---

# 8 · Bloque D · El sistema que rota

**Fases F5 + F6 + F7 + F8 · 6 sprints · el corazón del producto**

## Sprint 9 — Fatiga

| # | Tarea | Referencia |
|---|---|---|
| 9.1 | Reloj desde `Asignacion.inicio`, umbral **propio** del puesto | **A4** |
| 9.2 | **Exceso relativo en %** — nunca minutos absolutos entre puestos | **A4, B3** |
| 9.3 | Tres niveles; **los fijos no acumulan** | §9.1, §5.1 |
| 9.4 | La fatiga es del **puesto ocupado**, no de la categoría | **A7** |
| 9.5 | Factor de doble turno, **default 1.0** | **B7** |
| 9.6 | Barra de fatiga **continua** desde el minuto cero | §9.1 |

## Sprint 10 — Movimiento entre líneas

| # | Tarea | Referencia |
|---|---|---|
| 10.1 | Despacho con `hora_salida` | Parte X, §12.7 |
| 10.2 | **Tránsito inmune** — `UX_Mov_transito` | §6.1 |
| 10.3 | Recepción **individual** con `hora_llegada` | **C8**, §12.7 |
| 10.4 | Rechazo de recepción con **motivo obligatorio** | **C10** |
| 10.5 | Reserva de puesto — `UX_Mov_reserva` | **B4** |
| 10.6 | `sp_CaducarTransitos`: alerta y **no mueve a nadie** | **B11** |
| 10.7 | Pantalla de recepciones, un toque por persona | [03 §4.5](03_UIUX_BRIEF.md) |

## Sprint 11 — Cola de relevos y propuesta

| # | Tarea | Referencia |
|---|---|---|
| 11.1 | `SolicitudRelevo`; **el puesto NO se libera al avisar** | §9.4 p1 |
| 11.2 | Orden de cola: crítico → exceso relativo → FIFO | **B3** |
| 11.3 | **Aviso a todos los supervisores SIN identidad** | **D2**, §2.2 |
| 11.4 | `vw_SolicitudRelevo_L8` — proyección mínima | **D1** |
| 11.5 | `sp_ProponerRelevista` con el ranking exacto de B2 | **B2** |
| 11.6 | Pantalla del Bolsón — **sin malla de puestos** | **C7** |

## Sprint 12 — Ciclo de relevo y cadena

| # | Tarea | Referencia |
|---|---|---|
| 12.1 | Aceptar: tránsito + reserva atómicos + aviso al destino | §9.4 p3-4 |
| 12.2 | Rechazar: descarte del **par (puesto, persona)** | **B10** |
| 12.3 | Descartados visibles, limpiables y **caducan al cierre** | **B10** |
| 12.4 | Confirmar llegada → asignar → sugerir destino del relevado | §9.4 p5-6 |
| 12.5 | `sp_SugerirDestinoRelevado`: línea → **proximidad** → L8 | **B4, A1, A9** |
| 12.6 | Guarda: no sugerir un puesto ya reservado | **B4** |
| 12.7 | **Prueba automatizada del ejemplo normativo del §9.4** | **A8** |
| 12.8 | **Prueba de arquitectura: Relevos no referencia prioridad** | **A9** |

## Sprint 13 — Extracción inversa y piso de seguridad

| # | Tarea | Referencia |
|---|---|---|
| 13.1 | Orden **derivado** invirtiendo la prioridad vigente | **A5** |
| 13.2 | Solo con la L8 **completamente vacía** de candidatos | §9.6 |
| 13.3 | Piso por línea; línea en el mínimo = **inmune** | **B5** |
| 13.4 | *"Capacidad crítica de planta agotada"*, literal | §9.6 |
| 13.5 | Carga de **D9** | — |

## Sprint 14 — Vacante crítica en operación

| # | Tarea | Referencia |
|---|---|---|
| 14.1 | Escalera C15: N1 Bolsón → N2 misma línea → N3 otra línea → N4 | **C15** |
| 14.2 | N1 **encabeza la cola** de la L8 | B3, C15 |
| 14.3 | N3 **solo el Coordinador**, con justificación | **C15, A6** |
| 14.4 | **Guarda anti-dominó:** el rotativo descubierto va a prioridad normal | **C15** |
| 14.5 | `JustificacionExcepcion` + formularios de excepción | **A6** |
| 14.6 | Flujo de titular reincorporado | **C1** |
| 14.7 | Reincorporación desde retiro temporal | **C2** |

### 🎬 Demostración del bloque D

> **El ejemplo normativo del §9.4, completo.** Cinco puestos fatigados: cuatro en L4 y uno en L1. La L8 solo puede cubrir tres. Se envían dos a L4 y uno a L1. Al llegar los relevistas, los dos relevados **no van al Bolsón**: pasan a relevar a los otros dos puestos fatigados de L4. La fatiga de L4 se resuelve sin gastar más personal de la L8.
>
> Después se vacía la L8 a propósito y se comprueba que la extracción inversa respeta el piso. Y se retira un Operador A en operación para ver la escalera C15 en los cuatro niveles.

---

# 9 · Bloque E · El turno completo

**Fases F9 + F10 + F11 · 4 sprints**

## Sprint 15 — Contingencias

| # | Tarea | Referencia |
|---|---|---|
| 15.1 | Paro: dos niveles + **descripción obligatoria** | §11.1 |
| 15.2 | Fijos ocupados, rotativos liberados con tránsito individual | §11.1, C8 |
| 15.3 | **Cronómetro persistente en todas las pantallas** | §11.1 |
| 15.4 | Relevista en tránsito hacia línea en paro | **C9** |
| 15.5 | Cambio de SKU: recalcula puestos, cierra y abre lote | §11.2, C5 |
| 15.6 | Carga de **D10** | — |

## Sprint 16 — Lotes y estadística viva

| # | Tarea | Referencia |
|---|---|---|
| 16.1 | `Lote`; un solo lote abierto por línea | **C5** |
| 16.2 | Desperdicio por causa; justificación sobre umbral | §11.3 |
| 16.3 | Producción: cierre de lote + avances parciales | **C4** |
| 16.4 | **Eficiencia calculada en el servidor** | §11.4, **C4** |
| 16.5 | **Todo registro empuja a los dos paneles** | **C4** |
| 16.6 | *"Estimada desde el último registro — hace N min"* | C4, §12.4 |
| 16.7 | Panel de supervisor y panel de planta | §2.1.5 |
| 16.8 | Carga de **D11** | — |

## Sprint 17 — Tiempo real y notificaciones

| # | Tarea | Referencia |
|---|---|---|
| 17.1 | Hub SignalR con **grupos asignados por el servidor** | §2.2 |
| 17.2 | Eventos de [05 §2.4](05_TRD.md) | — |
| 17.3 | **FCM como campana vacía** + descarga del contenido real | **D5** |
| 17.4 | **Prueba que falla el build si la carga útil lleva negocio** | **D5**, §12.1 |
| 17.5 | Acuse, escalado y *"supervisor no localizable"* | **D5** |
| 17.6 | Sincronización de pendientes al volver al primer plano | D5 |
| 17.7 | Bandeja de salida transaccional | [05 §4.1](05_TRD.md) |

## Sprint 18 — Modo sin conexión

| # | Tarea | Referencia |
|---|---|---|
| 18.1 | Room + SQLCipher, clave en Keystore | **D3** |
| 18.2 | **Caché acotada a su línea** — nunca el padrón completo | **D3** |
| 18.3 | **Bloqueo defensivo: no se encola nada** | **§12.1** |
| 18.4 | Banner permanente con el texto literal del §12.1 | §12.1 |
| 18.5 | Sello de frescura + degradación visual | **D4** |
| 18.6 | Purga en los cinco disparadores | D3 |
| 18.7 | Detección por **latido**, no por estado del adaptador | [05 §4.3](05_TRD.md) |

### 🎬 Demostración del bloque E

> Se registra un paro y el tiempo aparece **al instante en los dos paneles, con el mismo número**. Se cierra un lote y la eficiencia se recalcula. Con la app **cerrada del todo**, llega una notificación de tránsito entrante. Se corta la red: la malla se sigue viendo con su sello de antigüedad, el modal de identidad sigue mostrando las restricciones médicas, y **ninguna escritura queda encolada**.

---

# 10 · Bloque F · Cierre y piloto

**Fase F12 · 3 sprints**

## Sprint 19 — Cierre y endurecimiento

| # | Tarea | Referencia |
|---|---|---|
| 19.1 | Cierre de turno con **lista exacta de bloqueos** | **C13** |
| 19.2 | Persistir `UltimaTareaJornada` al cerrar | **B6** |
| 19.3 | Cierre forzado del Coordinador con justificación | C13, A6 |
| 19.4 | Histórico y auditoría consultable | §2.1.11, §12.7 |
| 19.5 | Rendimiento contra los presupuestos de [05 §3.4](05_TRD.md) | — |
| 19.6 | Accesibilidad: 48 dp, contraste AAA, **escala de grises** | **A11**, §12.2 |
| 19.7 | Distribución del APK y verificación de versión | **F3** |

## Sprints 20–21 — Piloto en planta

| Etapa | Alcance | Duración | Criterio para avanzar |
|---|---|---|---|
| **P1** | 1 línea + la L8 | 3 turnos | Sin pérdida de personas · sin violaciones médicas · **el supervisor entiende los rechazos sin ayuda** |
| **P2** | 4 líneas + L8 | 5 turnos | Relevos entre líneas funcionando · tiempos de traslado registrados |
| **P3** | 10 líneas | 10 turnos | Los 5 Release Goals · **calibración de umbrales con datos reales** |

> **El piloto es donde se llenan los parámetros que se dejaron vacíos a propósito.** Umbrales de fatiga *(A4)*, duración de la ventana de arranque, piso de seguridad y `duracion_maxima_transito` *(B11)* salen de los tiempos de salida y llegada que §12.7 obliga a registrar — que es exactamente para lo que la especificación pidió ese dato.

### 🎬 Demostración final

> **Un turno real completo, sin papel.** Los cinco Release Goals del [PRD §3.1](01_PRD.md) verificados: cero personas en estado indeterminado al cierre, cero violaciones médicas en la auditoría, cero accesos fuera de alcance, informe de cierre generado sin intervención manual.

**Y el criterio que ningún tablero recoge** *(PRD §5.4)*:

> **Si el supervisor sigue llevando un papel en el bolsillo, el producto falló** — por muy verdes que estén los indicadores.

---

# 11 · Definición de terminado

## 11.1 Por tarea

- [ ] Código con pruebas unitarias
- [ ] Cita la sección de origen (`§x.y` o ID de decisión) en el código o en el PR
- [ ] Sin regla de negocio inventada — si faltó un dato, se preguntó
- [ ] CI en verde, incluida la suite de reglas de seguridad

## 11.2 Por sprint

- [ ] Todas las tareas terminadas
- [ ] Prueba de integración de lo nuevo
- [ ] Cobertura sobre el mínimo de [05 §6.4](05_TRD.md)
- [ ] Documentación actualizada si algo cambió
- [ ] **Registro de decisiones al día** si apareció una ambigüedad nueva

## 11.3 Por bloque

- [ ] Demostración ejecutada **delante del cliente**
- [ ] Criterios de salida de la fase del roadmap cumplidos
- [ ] Rendimiento dentro de presupuesto
- [ ] Suite de reglas de seguridad completa en verde

---

# 12 · Riesgos del plan

| Riesgo | Probabilidad | Mitigación |
|---|---|---|
| **D1 (vocabulario de capacidades) llega tarde o mal normalizado** | **Alta** | Se pide en el sprint 0 y se trabaja con Enfermería. Es la dependencia más silenciosa del proyecto |
| **Gafetes sin imprimir para el sprint 8** | Alta | Se arranca en el sprint 0. Se puede avanzar con QR en papel para pruebas, pero no pilotar |
| El bloque D se subestima | Media | Son 4 motores y 6 sprints. Es la mitad del esfuerzo real del proyecto |
| Se pide "meter todo junto" y saltar demostraciones | Media | Las demostraciones son el único punto donde el cliente puede corregir barato |
| El aislamiento se relaja "temporalmente" para depurar | Media | La suite de seguridad es bloqueante en cada PR, no solo antes de producción |
| Presión por añadir cola offline | Media | **§12.1 la prohíbe y explica por qué.** Documentado como antipatrón en [06 §3.3](06_ROADMAP.md) |
| Alucinación de reglas por IA | Media | Cláusula de no invención en cada prompt de motor *([06 §3.1](06_ROADMAP.md))* |
| Los motores se fusionan en un refactor | Baja | Prueba de arquitectura que falla la compilación *(A9)* |

---

# 13 · Qué se hace esta semana

**Sprint 0, en orden:**

1. **Confirmar los supuestos de §1.2** — equipo, duración de sprint, dedicación. Sin esto el plan no tiene calendario.
2. **Provisionar servidor y SQL Server** ⚠ `E3`.
3. **Conseguir tres teléfonos Android** ⚠ `E4`.
4. **Arrancar el diseño e impresión de los gafetes con QR** — es lo único que no depende del desarrollo.
5. **Convocar la sesión con Enfermería para D1** — el vocabulario de capacidades físicas.
6. **Entregar al cliente las plantillas de D2–D7.**
7. Crear el proyecto Firebase para el envío de FCM.

**Al terminar el sprint 0, empieza el sprint 1 y ya no hay nada que espere a nadie.**

---

# 14 · Trazabilidad del plan

| Bloque | Fases del roadmap | Documentos que lo gobiernan |
|---|---|---|
| Sprint 0 | — | E1, E3, E4, F2, F3 |
| A · Esqueleto seguro | F0, F1 | [04 §2, §6](04_ESQUEMA_BACKEND.md) · [05 §3.3](05_TRD.md) · A1, D6, §2.2, §2.3, §12.7 |
| B · Planta en datos | F2, F3 | [04 §2.7, §3, §7](04_ESQUEMA_BACKEND.md) · §7.1, §7.2, §7.3, A4, C12, C14 |
| C · Turno que arranca | F4 | [02 §4.1, §4.2](02_FLUJOS_DE_APP.md) · §8.1–§8.5, §12.2, B1, C5, C6, F3 |
| D · Sistema que rota | F5–F8 | [02 §4.3–§4.6](02_FLUJOS_DE_APP.md) · Parte IX, Parte X, A1, A5, A9, B2–B5, B10, B11, C1, C2, C15, D1, D2 |
| E · Turno completo | F9–F11 | [02 §4.7–§4.9, §5.1](02_FLUJOS_DE_APP.md) · Parte XI, §12.1, C4, C8, C9, D3, D4, D5 |
| F · Cierre y piloto | F12 | [01 §3.1, §5](01_PRD.md) · C13, B6, A11 |
