# SmartAssign — Plan de Implementación

**Dependencias, fases, encadenamiento de prompts y estrategia de QA y despliegue.**
Versión 1.0 · 2026-08-09

> **Principio de secuenciación.** No se construye nada cuyo cimiento no esté cerrado y probado. El ejemplo que gobierna todo este plan: **el motor de relevos no se puede construir sin el modelo de fatiga resuelto, y la fatiga no se puede modelar sin el modelo de personal y de puestos** — porque A4 puso los umbrales en el puesto y A7 hizo que la fatiga dependa de la asignación, no de la categoría.
>
> Cada fase termina en algo **verificable por una persona**, no en "código escrito".

---

# 1 · Orden de dependencias

## 1.1 Grafo

```
┌──────────────────────────────────────────────────────────────┐
│ F0 · CIMIENTOS                                               │
│ repositorio · esquema base · migraciones · semillas · CI      │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F1 · IDENTIDAD Y AISLAMIENTO                                 │
│ usuarios · JWT+PIN · RBAC · alcance por línea · RLS · auditoría│
│         ⚠ NADA se construye encima sin esto probado           │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F2 · MODELO DE PERSONAL Y PUESTOS                            │
│ padrón · categorías · capacidades · restricciones médicas     │
│ puestos con umbrales propios · titularidad · compatibilidad   │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F3 · MOTOR DE VALIDACIÓN                                     │
│ las 7 reglas · orden · primer rechazo detiene · mensajes      │
│         ⚠ ES LA PUERTA ÚNICA DE ESCRITURA                    │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F4 · ASIGNACIÓN Y JORNADA                                    │
│ turnos · jornada · prioridad · barrido de fijos · escalera    │
│ ventana de arranque · concurrencia                            │
└──────────────┬───────────────────────────────┬───────────────┘
               ▼                               ▼
┌──────────────────────────┐   ┌──────────────────────────────┐
│ F5 · FATIGA              │   │ F6 · MOVIMIENTO ENTRE LÍNEAS │
│ reloj por puesto         │   │ despacho·tránsito·recepción   │
│ exceso relativo          │   │ inmunidad · reserva · caducidad│
│ tres niveles             │   │ horas de salida y llegada     │
└──────────────┬───────────┘   └───────────────┬──────────────┘
               └───────────────┬───────────────┘
                               ▼
┌──────────────────────────────────────────────────────────────┐
│ F7 · MOTOR DE RELEVOS                                        │
│ proximidad · cola · ranking · descartados · cadena · L8       │
│    ⚠ imposible antes: necesita F5 (fatiga) + F6 (tránsito)   │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F8 · EXTRACCIÓN INVERSA Y VACANTE CRÍTICA                    │
│ orden invertido · piso de seguridad · escalera C15            │
│    ⚠ imposible antes: son excepciones al flujo normal de F7   │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F9 · CONTINGENCIAS Y ESTADÍSTICA                             │
│ paros · lotes · desperdicio · producción · eficiencia viva    │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F10 · TIEMPO REAL Y NOTIFICACIONES                           │
│ SignalR · grupos · FCM campana vacía · acuse · escalado       │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F11 · MODO SIN CONEXIÓN                                      │
│ caché cifrada · bloqueo defensivo · sello de frescura         │
└───────────────────────────┬──────────────────────────────────┘
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ F12 · CIERRE, HISTÓRICO Y ENDURECIMIENTO                     │
│ cierre de turno · auditoría consultable · rendimiento · piloto│
└──────────────────────────────────────────────────────────────┘
```

## 1.2 Por qué este orden y no otro

| Dependencia | Razón |
|---|---|
| **F1 antes que todo** | El aislamiento entre supervisores (§2.2) no es una capa que se añade: condiciona cada consulta. Añadirlo tarde obliga a reescribir todos los repositorios, y lo que se escapa son datos médicos |
| **F2 antes que F3** | Las reglas médicas necesitan el vocabulario `CapacidadFisica` compartido entre persona y puesto (§7.2). Sin él, la comparación no existe |
| **F3 antes que F4** | La validación es la **puerta única de escritura** ([04 §7.5](04_ESQUEMA_BACKEND.md)). Construir asignación antes crea un camino que la esquiva, y ese camino sobrevive para siempre |
| **F2 antes que F5** | **A4** puso los umbrales en el puesto y **A7** hizo que la fatiga dependa de la asignación. Sin el modelo de puestos y personal cerrado, el reloj de fatiga no tiene dónde apoyarse |
| **F5 y F6 antes que F7** | El relevo **es** fatiga (para saber a quién relevar) más tránsito (para moverlo). Es la dependencia que motivó este plan |
| **F7 antes que F8** | Extracción inversa y vacante crítica son **excepciones** al flujo normal. Construir la excepción antes que la norma produce un motor que no distingue el caso normal del extraordinario |
| **F9 después de F6** | Un paro genera tránsitos masivos hacia la L8 (§11.1, C8). Sin F6, el paro no puede liberar a nadie |
| **F10 después de F9** | Las notificaciones transportan eventos. Sin eventos que transportar, se prueba en vacío |
| **F11 casi al final** | El modo sin conexión necesita saber **qué** se cachea, y eso solo se sabe cuando las pantallas existen |

## 1.3 Dependencias externas — bloqueantes

| Dependencia | Bloquea | Cuándo hace falta |
|---|---|---|
| 🔴 **Gafetes impresos con QR** *(E1)* | Pruebas de campo de F4 | **Es una tarea física, no de software.** Diseño de etiqueta, impresión y distribución a ~160 personas tiene plazo propio y **no lo resuelve el equipo de desarrollo**. Hay que arrancarlo ya |
| ⚠ **E5 — salida a internet** | F10 | **Antes de empezar F10.** FCM necesita que los teléfonos alcancen los servidores de Google. Si la Wi-Fi de planta está aislada, hay que abrir salida o volver a una solución interna |
| Umbrales de fatiga *(A4)* | Calibración, **no construcción** | F5 se construye con umbrales configurables vacíos. Los valores llegan con el piloto |
| Horarios de turno *(C6)* | F4 | Dato de configuración, no de código |
| ⚠ E3, E4 | F0 (empaquetado) | Antes del primer despliegue a preproducción |
| ⚠ E7 — KPIs | Medición | No bloquea construcción |

> **Ya no bloquean nada:** E1 quedó cerrada (QR) y E2 desapareció como dependencia — la decisión de D5 eliminó el requisito de MDM. Lo único que quedó en su lugar es una dependencia **física**: imprimir los gafetes.

---

# 2 · Fases

Cada fase declara: **entra** (qué necesita) · **sale** (entregable) · **se verifica con** (criterio humano) · **no incluye** (para evitar arrastre de alcance).

## F0 · Cimientos

| | |
|---|---|
| **Entra** | Los 6 documentos aprobados |
| **Sale** | Repositorio con la estructura de [05 §5](05_TRD.md) · esquema base migrado · semillas cargadas · CI que compila y corre pruebas · entorno de preproducción |
| **Se verifica con** | Un desarrollador clona, ejecuta un comando y tiene base y API corriendo. La CI está en verde |
| **No incluye** | Ninguna regla de negocio |

**Semillas críticas:** 10 líneas con `es_bolson=1` en L8 · `PrioridadLinea` base · **`ProximidadLinea` completa con la corrección A1** · capacidades físicas · categorías y causas de paro · parámetros con los *a definir* deliberadamente vacíos.

> **La tabla de proximidad se siembra en F0 y se verifica en F0.** Es un dato de negocio corregido a mano (A1); si entra mal, el motor de relevos de F7 funcionará perfectamente enviando gente al sitio equivocado, y el fallo será invisible hasta que alguien camine de más.

## F1 · Identidad y aislamiento `[SEGURIDAD]`

| | |
|---|---|
| **Entra** | F0 |
| **Sale** | Login con AD y respaldo local · JWT + refresh · PIN · RBAC · **filtro de alcance por línea** · **RLS activa** · auditoría escribiendo · `/auth/me` resolviendo línea en vivo |
| **Se verifica con** | Un supervisor de L2 **no puede** obtener ni un solo dato de L4 por ningún endpoint. Se demuestra con la suite de aislamiento, no con una revisión visual |
| **No incluye** | Pantallas más allá del login |

**Criterio de salida bloqueante:**
- ✅ Cada endpoint existente rechaza el acceso fuera de alcance.
- ✅ La RLS bloquea aunque se salte el filtro de aplicación.
- ✅ La línea **no** está en el token *(§2.3)*.
- ✅ Toda operación deja traza *(§12.7)*.

> **Esta fase no se acorta.** §2.2 califica el aislamiento de "total y deliberado" y lo vincula a la protección de datos médicos. Es el único cimiento que, si se pospone, contamina todo lo construido encima.

## F2 · Modelo de personal y puestos

| | |
|---|---|
| **Entra** | F1 |
| **Sale** | Padrón CRUD · categorías · `CapacidadFisica` · **restricciones médicas con vigencia** · líneas · puestos con **umbrales propios** · titularidad de doble semántica · matriz de compatibilidad · pantallas de padrón del Coordinador |
| **Se verifica con** | El Coordinador da de alta a una persona con restricción temporal, y el sistema la considera activa hoy y no dentro de un mes |
| **No incluye** | Ninguna asignación |

**Puntos de atención:** `Personal.perfil` nulable significa *no evaluar*, nunca *no cumple* (§7.3) · restricciones **nunca se borran** (C14) · umbrales sembrados en `NULL` con caída al parámetro de planta (A4) · `titular_id` en ambos tipos con semántica distinta (C12).

## F3 · Motor de validación `[SEGURIDAD]`

| | |
|---|---|
| **Entra** | F2 |
| **Sale** | `sp_ValidarAsignacion` con las 7 reglas en orden · funciones de cada regla · mensajes en lenguaje de planta con `siguientePaso` · `DENY` aplicado sobre tablas críticas |
| **Se verifica con** | Se intenta asignar a alguien con restricción médica **por todos los caminos existentes** y falla en todos, con el mensaje correcto |
| **No incluye** | Motores que la consuman |

**Criterio de salida bloqueante:**
- ✅ El orden de las 7 reglas es el del §7.1 y el primer rechazo detiene.
- ✅ **Ningún parámetro puede saltar el paso 4** (médicas).
- ✅ `@permitir_ceder_perfil` afecta **solo** al paso 5.
- ✅ Cobertura **100 %** en `Domain/Validacion`.
- ✅ La cuenta de aplicación **no puede** escribir en `Asignacion` sin el procedimiento.

## F4 · Asignación y jornada

| | |
|---|---|
| **Entra** | F3 |
| **Sale** | Turnos y día de operación · planificación · `PrioridadLinea` versionada · **barrido de puestos fijos** · escalera §8.5 · **ventana de arranque** · concurrencia con idempotencia · escaneo y confirmación de identidad · malla de línea |
| **Se verifica con** | Un turno arranca de verdad: el barrido cubre los fijos por prioridad, deja los rotativos vacíos, y un supervisor llena su línea desde el teléfono |
| **No incluye** | Fatiga, relevos, movimientos entre líneas |

**Criterio de salida:**
- ✅ El barrido recorre por prioridad y **solo toca `tipo='fijo'`**.
- ✅ Conserva `titular_original_id` al usar suplente *(§8.3)*.
- ✅ Genera vacante crítica cuando corresponde.
- ✅ La ventana bloquea a quien no está físicamente en la línea.
- ✅ Dos supervisores capturando a la misma persona: **exactamente un ganador**, con mensaje nominal *(B1)*.
- ✅ El escaneo **nunca** asienta sin el modal de identidad *(§12.2)*.

⚠ **Las pruebas de campo requieren los gafetes ya impresos con QR** *(E1)*. La construcción puede avanzar con QR generados en papel para pruebas, pero el piloto no.

## F5 · Modelo de fatiga

| | |
|---|---|
| **Entra** | F4 |
| **Sale** | Reloj desde `Asignacion.inicio` · umbral **propio** por puesto con caída a default · **exceso relativo en %** · tres niveles · factor de doble turno · avance continuo en la interfaz |
| **Se verifica con** | Dos puestos con umbrales distintos y el mismo tiempo ocupado muestran niveles distintos, correctamente |
| **No incluye** | Relevos |

**Criterio de salida:**
- ✅ La fatiga cuelga de la **asignación**, no de la persona ni de la categoría *(A7)*.
- ✅ Los puestos fijos **no** acumulan *(§5.1)*.
- ✅ El avance se ve de forma **continua**, no solo al cruzar *(§9.1)*.
- ✅ Todo ordenamiento usa **exceso relativo**, nunca minutos absolutos *(A4, B3)*.

> **Aquí se hace visible por qué F2 iba antes.** El reloj de fatiga no tiene sentido sin un puesto que declare su propio umbral y una asignación que marque el inicio. Construir esta fase antes habría producido un modelo global que A4 invalidó.

## F6 · Movimiento entre líneas

| | |
|---|---|
| **Entra** | F4 |
| **Sale** | Despacho · tránsito con **inmunidad** · recepción **individual** · rechazo con motivo obligatorio · reserva de puesto · **caducidad de tránsito** · `hora_salida`/`hora_llegada`/`duracion_seg` |
| **Se verifica con** | Una persona se mueve de L2 a L8, no puede ser capturada durante el trayecto, y quedan registradas las dos horas |
| **No incluye** | Relevos |

**Criterio de salida:**
- ✅ `UX_Mov_transito`: nadie en dos tránsitos *(§6.1)*.
- ✅ `UX_Mov_reserva`: ningún puesto reservado dos veces *(B4)*.
- ✅ El tránsito caducado **alerta y no mueve a nadie** *(B11)*.
- ✅ Rechazo de recepción → **tránsito hacia L8**, no salto directo *(C10)*.
- ✅ Recepción **individual, persona por persona** *(C8)*.

## F7 · Motor de relevos

| | |
|---|---|
| **Entra** | **F5 + F6** |
| **Sale** | `ProximidadLinea` en uso · cola con orden B3 · ranking B2 · aceptar/rechazar · descartados con caducidad · **reasignación en cadena del relevado** · pantalla exclusiva del Bolsón · proyección D1 |
| **Se verifica con** | **El ejemplo normativo del §9.4 completo**: 5 puestos fatigados, la L8 cubre 3, los relevados se encadenan sin gastar más personal del Bolsón |
| **No incluye** | Extracción inversa, vacante crítica |

**Criterio de salida bloqueante:**
- ✅ El motor **no referencia** el servicio de prioridad — verificado por prueba de arquitectura *(A9)*.
- ✅ Usa la tabla de proximidad **con la corrección A1**.
- ✅ La L8 **nunca** ve nombre ni restricciones médicas de personal ajeno *(D1)*.
- ✅ El aviso a todos los supervisores va **sin identidad** *(D2)*.
- ✅ Los descartados **caducan al cierre de turno** *(B10)*.
- ✅ El puesto **no se libera** al avisar *(§9.4 p1)*.
- ✅ El ejemplo del §9.4 pasa como prueba automatizada.

> **Es la fase más grande y la que más depende de que las anteriores estén bien.** Si la fatiga da niveles equivocados o el tránsito pierde la reserva, el relevo falla de formas que parecen aleatorias en planta.

## F8 · Extracción inversa y vacante crítica

| | |
|---|---|
| **Entra** | F7 |
| **Sale** | Orden invertido derivado *(A5)* · **piso de seguridad por línea** · alerta de planta agotada · escalera C15 N1–N4 · excepción de hub-and-spoke acotada · formulario de justificación |
| **Se verifica con** | Se vacía la L8 a propósito y el sistema extrae de la línea de menor prioridad respetando el piso; y un Operador A se retira y la escalera C15 responde en los cuatro niveles |
| **No incluye** | — |

**Criterio de salida:**
- ✅ La extracción **solo** se activa con la L8 completamente vacía de candidatos viables *(§9.6)*.
- ✅ Una línea en su mínimo es **inmune** *(B5)*.
- ✅ Planta agotada → mensaje literal del §9.6.
- ✅ C15-N3 **solo** lo ejecuta el Coordinador, con justificación *(A6)*.
- ✅ **Guarda anti-dominó:** el rotativo descubierto entra a prioridad normal, no como emergencia *(C15)*.

## F9 · Contingencias y estadística

| | |
|---|---|
| **Entra** | F6 |
| **Sale** | Paros con clasificación de dos niveles y descripción obligatoria · **cronómetro persistente** · liberación de rotativos con tránsitos individuales · lotes · cambio de SKU · desperdicio con umbral · producción · **eficiencia calculada en el servidor** · paneles de supervisor y Coordinador |
| **Se verifica con** | Se registra un paro y el tiempo acumulado aparece **al instante en los dos paneles**, con el mismo número |
| **No incluye** | Difusión en vivo (es F10) |

**Criterio de salida:**
- ✅ Los fijos permanecen ocupados durante el paro; los rotativos se liberan *(§11.1)*.
- ✅ El cronómetro es visible **desde cualquier pantalla** *(§11.1)*.
- ✅ La eficiencia usa el ritmo teórico **del catálogo** *(§11.4)*.
- ✅ **El cálculo vive en el servidor**: los dos paneles nunca divergen *(C4)*.
- ✅ Sin registro reciente, la interfaz dice *"estimada desde el último registro"*, nunca un número inventado *(C4)*.

## F10 · Tiempo real y notificaciones

| | |
|---|---|
| **Entra** | F9 · ⚠ **E5 confirmado** (salida a internet para FCM) |
| **Sale** | SignalR con grupos por alcance · eventos · **FCM como campana vacía** · descarga del contenido real desde el servidor · **acuse y escalado** · panel *"supervisor no localizable"* |
| **Se verifica con** | Un supervisor **con la app cerrada** recibe la notificación de un tránsito entrante; y una notificación crítica sin acuse aparece escalada en el panel del Coordinador |
| **No incluye** | — |

**Criterio de salida bloqueante:**
- ✅ **La carga útil de FCM no contiene ningún campo de negocio** — verificado por prueba que inspecciona el objeto enviado *(§12.1, D5)*.
- ✅ Ninguna llamada de red fuera del servidor de planta y los extremos de FCM.
- ✅ La suscripción a grupos la asigna el servidor; el cliente **no puede pedirla** *(§2.2)*.
- ✅ `AvisoFatigaPlanta` **sin identidad** — verificado por prueba de carga útil *(D2)*.
- ✅ Toda notificación crítica sin acuse **escala** *(D5)*.
- ✅ Entrega verificada **con la app forzada a cerrarse**, no solo en segundo plano.

## F11 · Modo sin conexión

| | |
|---|---|
| **Entra** | F10 |
| **Sale** | Room + SQLCipher con clave en Keystore · **alcance de caché limitado a su línea** · bloqueo defensivo de escrituras · banner permanente · **sello de frescura** con degradación · purga en los cinco disparadores |
| **Se verifica con** | Se corta la red: la malla se sigue viendo con su sello, el modal de identidad sigue mostrando restricciones médicas, y ninguna escritura se encola |
| **No incluye** | Cola optimista — **prohibida por diseño** |

**Criterio de salida:**
- ✅ **No existe cola de operaciones pendientes** *(§12.1)*.
- ✅ La caché **no contiene el padrón completo** *(D3)*.
- ✅ El dispositivo del Coordinador **no cachea restricciones médicas**.
- ✅ La detección usa **latido contra el servidor**, no estado del adaptador.
- ✅ Dato viejo **visiblemente degradado**, nunca presentado como vivo *(D4)*.

## F12 · Cierre, histórico y endurecimiento

| | |
|---|---|
| **Entra** | F11 |
| **Sale** | Cierre de turno con verificación de bloqueos · `UltimaTareaJornada` · caducidad de descartados · cierre forzado con justificación · histórico · auditoría consultable · rendimiento contra presupuestos · accesibilidad verificada · **piloto en planta** |
| **Se verifica con** | **Un turno real completo, sin papel** |
| **No incluye** | — |

**Criterio de salida — es el criterio de lanzamiento:**
- ✅ Los 5 Release Goals del [PRD §3.1](01_PRD.md).
- ✅ Cero personas en estado indeterminado al cierre.
- ✅ Cero violaciones médicas en la auditoría del piloto.
- ✅ Cero accesos fuera de alcance.
- ✅ Latencias dentro de presupuesto *(05 §3.4)*.
- ✅ Zonas de toque ≥ 56 dp, contraste AAA, **app operable en escala de grises**.
- ✅ Suite de reglas de seguridad **completamente en verde**.

---

# 3 · Encadenamiento de prompts (Prompt Chaining)

> **Para qué sirve esta sección.** Buena parte de la construcción se hará asistida por IA. Un prompt que pide demasiado, o que no declara de qué documento sale cada regla, produce **alucinación de reglas de negocio** — que en este sistema significa inventar un umbral de fatiga, cambiar el orden de las siete validaciones o relajar una restricción médica.
>
> Cada bloque declara: **lee** (contexto exacto) · **produce** (artefactos) · **verifica** (criterio) · **NO necesita** (lo que se excluye para no contaminar).

## 3.1 Las cuatro reglas del encadenamiento

**Regla 1 — Un prompt, un artefacto verificable.** Nunca "implementa el motor de relevos". Sí "implementa `sp_ProponerRelevista` con el ranking de B2".

**Regla 2 — El contexto se declara, no se asume.** Todo prompt empieza nombrando qué secciones lee. Si una regla no está en ese contexto, **el prompt debe fallar, no improvisar**.

**Regla 3 — Prohibición explícita de inventar.** Todo prompt de motor lleva esta cláusula literal:

> *"Si necesitas un valor, un umbral, una jerarquía o un criterio de desempate que no esté en el contexto proporcionado, **DETENTE y pregunta**. No lo infieras, no lo estimes, no uses un valor 'razonable'. Los valores de negocio de este sistema afectan a la seguridad ocupacional de 160 personas."*

**Regla 4 — El artefacto anterior es contexto del siguiente.** Nunca se re-deriva lo ya construido.

## 3.2 Cadena por fase

### Cadena F1 — Identidad y aislamiento

```
P1.1  LEE      04 §6.1, 05 §3.3, D6
      PRODUCE  Usuario, SesionDispositivo, migración
      VERIFICA la migración corre y revierte
      NO NECESITA nada de motores ni de puestos

P1.2  LEE      P1.1 + 04 §6.4 + D6
      PRODUCE  servicio de autenticación: JWT, refresh, PIN, AD con respaldo local
      VERIFICA login válido/inválido, refresh, 3 PIN fallidos
      ⚠ RESTRICCIÓN LITERAL: el token NO lleva linea_id (§2.3)

P1.3  LEE      P1.2 + 04 §6.2 + §2.2 + §2.3
      PRODUCE  filtro de autorización por rol + filtro de alcance por línea
      VERIFICA supervisor de L2 no accede a L4 en ningún endpoint
      ⚠ la línea se resuelve EN VIVO desde Linea.supervisor_actual

P1.4  LEE      P1.3 + 04 §6.3
      PRODUCE  RLS: fn_AlcanceLinea + política
      VERIFICA bloquea aunque se salte el filtro de aplicación

P1.5  LEE      04 §8 + §12.7
      PRODUCE  Auditoria + sp_RegistrarAuditoria, incluidos rechazos
      VERIFICA toda operación deja traza; los rechazos también
```

### Cadena F3 — Validación `[la más delicada]`

```
P3.1  LEE      §7.2 + 04 §2.7 + 04 §3.2 + C14
      PRODUCE  fn_TieneRestriccionBloqueante
      VERIFICA restricción vigente bloquea; caducada no; permanente siempre
      ⚠ CLÁUSULA DE NO INVENCIÓN ACTIVA

P3.2  LEE      §4.2 + A7 + A7b
      PRODUCE  fn_CategoriaCompatible
      VERIFICA la matriz completa del §4.2, casilla por casilla
      ⚠ NO inventes casillas. Si una combinación no está en §4.2, pregunta.

P3.3  LEE      §7.3 + B12
      PRODUCE  fn_PerfilIncompatible
      ⚠ si Personal.perfil es NULL, la regla NO se aplica. Nunca se infiere.

P3.4  LEE      §7.4 + A4 + B6 + 04 §3.4
      PRODUCE  fn_ViolaNoRepeticion24h
      ⚠ SOLO la actividad marcada con aplica_no_repeticion_24h
      ⚠ ventana = jornada trabajada anterior, NO día calendario

P3.5  LEE      §8.4 + 04 §4.1
      PRODUCE  fn_VentanaArranqueBloquea
      ⚠ la ventana la evalúa el SERVIDOR, nunca el dispositivo

P3.6  LEE      P3.1..P3.5 + §7.1 + B12 + 04 §7.2
      PRODUCE  sp_ValidarAsignacion
      VERIFICA orden exacto 1..7, primer rechazo detiene
      ⚠ NINGÚN parámetro puede saltar el paso 4
      ⚠ @permitir_ceder_perfil afecta SOLO al paso 5

P3.7  LEE      P3.6 + §1.3 + §12.4 + 02 §5.4
      PRODUCE  catálogo de mensajes con detail + siguientePaso
      ⚠ usa el catálogo de 02 §5.4 LITERAL. No redactes mensajes nuevos.

P3.8  LEE      P3.6 + 05 §6.2
      PRODUCE  suite de reglas de seguridad: médicas × 8 caminos
      VERIFICA todos fallan correctamente. Es criterio de salida de fase.
```

### Cadena F5 — Fatiga

```
P5.1  LEE      §9.1 + A4 + A7 + 04 §2.6 + 04 §5.1
      PRODUCE  cálculo de fatiga desde Asignacion.inicio con umbral propio
      ⚠ NO inventes umbrales por defecto. Se leen de Parametro y pueden estar vacíos.
      ⚠ la fatiga es del PUESTO OCUPADO, no de la categoría (A7)
      ⚠ los puestos fijos NO acumulan (§5.1)

P5.2  LEE      P5.1 + A4 + B3
      PRODUCE  cálculo de exceso relativo en %
      ⚠ nunca compares minutos absolutos entre puestos distintos

P5.3  LEE      P5.2 + §11.5 + B7
      PRODUCE  factor de doble turno, con default 1.0
      ⚠ default 1.0 = informativo. NO inventes otro factor.

P5.4  LEE      P5.2 + 03 §2.1 + §9.1
      PRODUCE  barra de fatiga relativa y continua en Compose
      ⚠ avance continuo desde el minuto cero, no solo al cruzar
```

### Cadena F7 — Relevos `[la más grande]`

```
P7.1  LEE      §9.5 + A1 + A2 + A3 + 04 §2.3
      PRODUCE  repositorio de proximidad + semilla completa
      VERIFICA la fila de L10 es L9,L3,L6,L7,L4,L2,L1,L5,L8
      ⚠ grafo DIRIGIDO. La asimetría es intencional. No la "corrijas".

P7.2  LEE      §9.4 p1 + B3 + B9 + D2 + 04 §5.3
      PRODUCE  SolicitudRelevo + detección de umbral + cola ordenada
      ⚠ el puesto NO se libera al avisar (§9.4 p1)
      ⚠ el aviso a todos los supervisores va SIN IDENTIDAD (D2)
      ⚠ NO ordenes por prioridad de línea (A9)

P7.3  LEE      §9.4 p2 + B2 + B10 + D1 + 04 §6.3
      PRODUCE  sp_ProponerRelevista + vw_SolicitudRelevo_L8
      ⚠ la vista NO expone personal_id, nombre, ficha ni médicas ajenas (D1)
      ⚠ el ranking es EXACTAMENTE el de B2. No añadas criterios.

P7.4  LEE      P7.3 + §9.4 p3 + B10 + F6
      PRODUCE  sp_AceptarRelevo, sp_RechazarRelevo
      ⚠ el descarte es del PAR (puesto, persona) y caduca al cierre de turno

P7.5  LEE      P7.4 + §9.4 p5-6 + B4 + A1 + A9
      PRODUCE  sp_ConfirmarRecepcion + sp_SugerirDestinoRelevado
      ⚠ misma línea → proximidad → L8. NUNCA prioridad (A9).
      ⚠ guarda: el puesto destino no puede estar ya reservado (B4)
      ⚠ mismo destino en la misma línea = asignación directa, sin tránsito

P7.6  LEE      P7.5 + §9.4 ejemplo normativo + A8
      PRODUCE  prueba automatizada del ejemplo completo
      VERIFICA 5 fatigados, L8 cubre 3, los relevados se encadenan
      ⚠ el ejemplo del §9.4 es NORMATIVO (A8). Debe reproducirse exacto.

P7.7  LEE      P7.3 + C7 + 03 §4.5 + 02 §1.3
      PRODUCE  pantalla del Bolsón: recepciones arriba, cola, personal
      ⚠ el Bolsón NO tiene malla de puestos (C7)

P7.8  LEE      todo F7 + A9 + 05 §4.1
      PRODUCE  prueba de arquitectura: Relevos no referencia prioridad
      VERIFICA falla la compilación si la dependencia aparece
```

### Cadenas restantes — resumen

| Cadena | Bloques | Restricciones literales clave |
|---|---|---|
| **F0** | P0.1 estructura · P0.2 esquema · P0.3 semillas · P0.4 CI | La proximidad se siembra **con la corrección A1** y se verifica en F0 |
| **F2** | P2.1 padrón · P2.2 capacidades · P2.3 médicas · P2.4 puestos · P2.5 pantallas | `perfil` NULL = no evaluar · médicas nunca se borran · umbrales pueden ser NULL |
| **F4** | P4.1 turnos · P4.2 planificación · P4.3 prioridad · P4.4 **barrido** · P4.5 escalera · P4.6 ventana · P4.7 concurrencia · P4.8 escaneo · P4.9 malla | El barrido **solo** `tipo='fijo'` · conserva titular · escalera de 4 niveles exacta · médicas no ceden en ninguno |
| **F6** | P6.1 despacho · P6.2 inmunidad · P6.3 recepción · P6.4 rechazo · P6.5 caducidad | Recepción **individual** (C8) · rechazo → tránsito a L8 (C10) · caducidad **no mueve a nadie** (B11) |
| **F8** | P8.1 orden derivado · P8.2 piso · P8.3 escalera C15 · P8.4 justificación | Solo con L8 **completamente** vacía · guarda anti-dominó · N3 solo Coordinador |
| **F9** | P9.1 paros · P9.2 cronómetro · P9.3 lotes · P9.4 desperdicio · P9.5 producción · P9.6 eficiencia · P9.7 paneles | Fijos ocupados en paro · cronómetro en **todas** las pantallas · cálculo **en servidor** (C4) |
| **F10** | P10.1 hub · P10.2 grupos · P10.3 eventos · P10.4 servicio · P10.5 acuse | Grupos asignados por servidor · `AvisoFatigaPlanta` sin identidad · **sin terceros** |
| **F11** | P11.1 caché cifrada · P11.2 bloqueo · P11.3 sello · P11.4 purga | **Prohibido** implementar cola de escritura · alcance de caché limitado |
| **F12** | P12.1 cierre · P12.2 histórico · P12.3 rendimiento · P12.4 accesibilidad · P12.5 piloto | Cierre bloqueado con lista exacta · `UltimaTareaJornada` al cerrar |

## 3.3 Antipatrones prohibidos

| Antipatrón | Por qué falla aquí |
|---|---|
| *"Implementa el motor de relevos"* | Demasiado grande. La IA rellena huecos inventando criterios de desempate |
| *"Usa un umbral razonable de fatiga"* | **A4 dice que se calibra con datos reales.** Cualquier valor inventado se convierte en dato de negocio sin que nadie lo decida |
| *"Optimiza el orden de las validaciones"* | §7.1 fija el orden para que el mensaje sea el correcto. Reordenar cambia qué se le dice al supervisor |
| *"Unifica los motores en uno parametrizado"* | **A9 lo prohíbe.** Filtra la prioridad a decisiones de relevo |
| *"Añade una cola offline para mejorar la experiencia"* | **§12.1 la prohíbe explícitamente** y explica por qué |
| *"Simplifica: manda todos los datos y filtra en el cliente"* | Viola §2.2. Los datos médicos llegarían al dispositivo equivocado |
| *"Completa la tabla de proximidad por simetría"* | **A3: la asimetría es intencional** |
| *"Corrige el mensaje de error para que sea más claro"* | La micro-copia del §12.5 es literal y no se reescribe |

---

# 4 · Estrategia de QA y despliegue

## 4.1 Calidad por fase

| Fase | Unitarias | Integración | Seguridad | Accesibilidad | E2E |
|---|---|---|---|---|---|
| F1 | ✓ | ✓ | **✓ bloqueante** | — | — |
| F2 | ✓ | ✓ | ✓ | ✓ | — |
| F3 | **✓ 100 %** | ✓ | **✓ bloqueante** | — | — |
| F4 | ✓ | ✓ | ✓ | ✓ | ✓ arranque |
| F5 | ✓ | ✓ | — | ✓ | — |
| F6 | ✓ | ✓ | ✓ | ✓ | ✓ traslado |
| F7 | **✓ 95 %** | ✓ | **✓ bloqueante** | ✓ | **✓ ejemplo §9.4** |
| F8 | ✓ | ✓ | ✓ | — | ✓ |
| F9 | ✓ | ✓ | — | ✓ | ✓ paro y lote |
| F10 | ✓ | ✓ | **✓ bloqueante** | — | ✓ |
| F11 | ✓ | ✓ | **✓ bloqueante** | ✓ | ✓ sin red |
| F12 | ✓ | ✓ | **✓ completa** | **✓ completa** | **✓ turno completo** |

## 4.2 Puertas de calidad

**Puerta 1 — cada commit:** compila · unitarias en verde · lint · pruebas de arquitectura · sin secretos.

**Puerta 2 — cada PR:** integración en verde · cobertura sobre mínimo · **suite de reglas de seguridad completa** · escaneo de dependencias · revisión humana.

**Puerta 3 — cada fase:** todos los criterios de salida · E2E de la fase · rendimiento dentro de presupuesto · documentación actualizada · **registro de decisiones al día**.

**Puerta 4 — producción:** los 5 Release Goals · piloto de un turno completo sin papel · plan de reversión probado · **aprobación humana explícita**.

> **La suite de reglas de seguridad es bloqueante en la Puerta 2, no solo en la 4.** Un fallo en una regla médica no puede llegar a integrarse esperando a que alguien lo detecte antes del despliegue.

## 4.3 CI/CD

```
COMMIT → compilar · unitarias · lint · arquitectura · secretos
   ↓
PR     → integración (SQL en contenedor) · SEGURIDAD ⛔ · cobertura · dependencias · revisión
   ↓
MAIN   → artefacto backend + APK firmado → despliegue automático a PREPRODUCCIÓN
   ↓
PREPRO → E2E · rendimiento · accesibilidad · prueba manual exploratoria
   ↓
PROD   → aprobación manual
         → ventana ENTRE TURNOS (verificar: 0 tránsitos, 0 lotes abiertos)
         → respaldo → migración → despliegue → verificación de salud
         → reversión automática si falla
```

## 4.4 Despliegue a producción

**Ventana obligatoria entre turnos.** Verificación previa automatizada:

```sql
-- Ambas deben devolver 0
SELECT COUNT(*) FROM Movimiento    WHERE estado = 'en_transito';
SELECT COUNT(*) FROM Lote          WHERE cerrado_en IS NULL;
SELECT COUNT(*) FROM JornadaLinea  WHERE estado = 'arrancada';
```

> **Por qué es innegociable:** desplegar con gente en tránsito significa reiniciar el servidor mientras una persona camina entre dos líneas con su destino comprometido. Al volver, o su tránsito sigue coherente o hay alguien perdido en la planta — exactamente el problema del §1.1.

**Migraciones:** patrón de expansión y contracción en tres despliegues *(04 §11.2)*, porque más de 160 dispositivos no actualizan el mismo día *(Anexo §3)*.

**Reversión:** artefacto anterior siempre disponible · reversión de migración probada en preproducción · el APK anterior sigue funcionando mientras el API mantenga compatibilidad `v1`.

## 4.5 Piloto en planta

Antes del lanzamiento completo:

| Etapa | Alcance | Duración | Criterio para avanzar |
|---|---|---|---|
| **P1** | 1 línea + la L8 | 3 turnos | Sin pérdida de personas · sin violaciones médicas · el supervisor entiende los rechazos sin ayuda |
| **P2** | 4 líneas + L8 | 5 turnos | Relevos entre líneas funcionando · tiempos de traslado registrados |
| **P3** | 10 líneas | 10 turnos | Los 5 Release Goals · **calibración de umbrales de fatiga con datos reales (A4)** |

> **El piloto es donde se resuelven los parámetros que quedaron vacíos a propósito.** Los umbrales de fatiga (A4), la duración de la ventana de arranque, el piso de seguridad y `duracion_maxima_transito` (B11) se llenan con los datos de §12.7 recogidos en P1 y P2 — que es exactamente para lo que la especificación pidió registrar las horas de salida y llegada.

**Criterio de fracaso del piloto**, del [PRD §5.4](01_PRD.md):

> **Si el supervisor sigue llevando un papel en el bolsillo, el producto falló** — por muy verdes que estén los indicadores.

---

# 5 · Riesgos

| Riesgo | Impacto | Mitigación |
|---|---|---|
| **Gafetes sin imprimir a tiempo** | Bloquea las pruebas de campo de F4 y el piloto | **Arrancar la impresión ya.** Es tarea física con plazo propio, ajena al desarrollo. Especificación en [00 §E1](00_DECISIONES.md) |
| **Wi-Fi de planta sin salida a internet** *(E5)* | FCM no entrega — bloquea F10 | Confirmarlo antes de F10. Si está aislada: abrir salida hacia FCM o volver a una solución interna |
| **Alguien "mejora" la notificación añadiendo el nombre** | Sacaría datos de personal hacia un tercero | Prueba que inspecciona la carga útil de FCM y falla el build si aparece cualquier campo de negocio |
| Umbrales sin calibrar | F5 y F7 se prueban con valores provisionales | Configurables desde el diseño. Se calibran en el piloto |
| El aislamiento se relaja "temporalmente" para depurar | Fuga de datos médicos | La suite de seguridad es bloqueante en cada PR, no solo antes de producción |
| Presión por añadir cola offline | Contradice §12.1 | Documentado como antipatrón prohibido en §3.3 |
| Los motores se fusionan en un refactor | Viola A9 | Prueba de arquitectura que falla la compilación |
| La proximidad se siembra sin la corrección A1 | Relevos enviados al sitio equivocado, en silencio | Verificación explícita en F0 |
| Alucinación de reglas por IA | Regla de negocio inventada | Cláusula de no invención en cada prompt de motor |

---

# 6 · Resumen de secuencia

```
F0  Cimientos ......................... base, semillas (¡A1!), CI
F1  Identidad y aislamiento ........... ⛔ bloqueante para todo
F2  Personal y puestos ................ vocabulario médico, umbrales
F3  Validación ........................ ⛔ puerta única de escritura
F4  Asignación y jornada .............. barrido, escalera, ventana  [gafetes QR]
F5  Fatiga ............................ reloj por puesto, exceso relativo
F6  Movimiento ........................ tránsito, reserva, caducidad
F7  Relevos ........................... requiere F5 + F6
F8  Extracción inversa y C15 .......... excepciones al flujo normal
F9  Contingencias y estadística ....... paros, lotes, eficiencia
F10 Tiempo real y notificaciones ...... SignalR + FCM campana vacía    [⚠E5]
F11 Sin conexión ...................... caché cifrada, bloqueo defensivo
F12 Cierre, histórico, piloto ......... criterio de lanzamiento
```

---

# 7 · Trazabilidad

| Decisión de plan | Origen |
|---|---|
| F1 antes que todo | §2.2 |
| F3 como puerta única | §7 (encabezado), [04 §7.5](04_ESQUEMA_BACKEND.md) |
| F2 antes que F5 | A4, A7 |
| F5 + F6 antes que F7 | §9.4, Parte X |
| F7 antes que F8 | §9.6, C15 |
| Motores separados verificados | A9 |
| Prueba del ejemplo §9.4 | §9.4, A8 |
| Semilla de proximidad en F0 | A1, A3 |
| Umbrales calibrados en piloto | A4, §12.7 |
| Sin cola offline | §12.1 |
| Recepción individual | C8 |
| Cálculo en servidor | C4 |
| FCM campana vacía · sin MDM | D5, E2, F3 |
| Gafetes con QR como dependencia física | E1 |
| Coordinador en teléfono | F4 |
| Ventana entre turnos | §1.1, [04 §11.4](04_ESQUEMA_BACKEND.md) |
| Expansión y contracción | Anexo §3 |
| Criterio de fracaso del piloto | [PRD §5.4](01_PRD.md) |
