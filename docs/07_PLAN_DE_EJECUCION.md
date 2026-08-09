# SmartAssign — Plan de Ejecución

**Diseñado para ser ejecutado por un agente de IA con revisión humana.**
Versión 2.0 · 2026-08-09

> **Por qué esta versión reemplaza a la anterior.** La v1.0 estaba organizada en sprints semanales, con demostraciones y ceremonias — la forma correcta para un equipo humano. El ejecutor es un agente de IA, y eso cambia las tres cosas que estructuran un plan:
>
> | | Equipo humano | Agente de IA |
> |---|---|---|
> | **Recurso escaso** | Tiempo | **Contexto** |
> | **Unidad de trabajo** | Sprint (una semana) | **UT: lo que cabe en una sesión y termina en verde** |
> | **Verificación** | Demostración al cliente | **Un comando que pasa o falla, sin interpretación** |
>
> El plan de fases y dependencias del [Roadmap](06_ROADMAP.md) **no cambia**: el orden de construcción sigue siendo el mismo y por las mismas razones. Lo que cambia es cómo se trocea y cómo se comprueba.

---

# 1 · Reparto de responsabilidades

## 1.1 Lo que ejecuta el agente

Todo el código: backend, base de datos, Android, migraciones, semillas, pruebas. Y la actualización de los documentos cuando algo cambia.

## 1.2 Lo que solo puede hacer el humano

| # | Tarea | Cuándo |
|---|---|---|
| H1 | Provisionar servidor y SQL Server de planta | Antes del primer despliegue real |
| H2 | Probar en **teléfonos físicos** (mínimo 3) | Etapa E6 en adelante |
| H3 | Verificar la red de planta | Antes de E12 |
| H4 | **Imprimir los gafetes con QR** | Antes de las pruebas de campo |
| H5 | Aportar los **datos reales** de personal, puestos, SKU | Antes de producción |
| H6 | Sesión con Enfermería: vocabulario de capacidades físicas | Antes de producción |
| H7 | Cerrar las decisiones que siguen abiertas (A5b, A7-orig) | Antes de E10 |
| H8 | **Aprobar los puntos de control** | Ver §6 |
| H9 | Ejecutar el piloto en planta | Etapa final |

> **Ninguna de estas bloquea el arranque.** Con datos simulados *(§4)*, la construcción avanza de E1 a E14 sin esperar a nadie. Lo real entra al final, por un importador diseñado para eso.

---

# 2 · La unidad de trabajo (UT)

Cada UT es la pieza atómica del plan. Está dimensionada para **caber en una sesión y terminar con una verificación en verde**.

```
UT-<etapa>.<n> · <objetivo en una frase>
  LEE       secciones exactas — el presupuesto de contexto
  PRODUCE   ficheros exactos
  VERIFICA  el comando que debe pasar
  NO LEE    lo que se excluye a propósito
```

## 2.1 Las cinco reglas de ejecución

**R1 · Una UT termina en verde o no termina.** No existe "creo que funciona". Si el comando de verificación falla, la UT sigue abierta.

**R2 · Cláusula de no invención.** Literal, activa en toda UT de motor:

> *Si necesitas un valor, un umbral, una jerarquía o un criterio de desempate que no esté en las secciones declaradas en LEE, **detente y pregunta**. No lo infieras, no lo estimes, no uses un valor "razonable". Los valores de negocio de este sistema afectan a la seguridad ocupacional de 160 personas.*

**R3 · El contexto se declara, no se asume.** Si una regla no está en `LEE`, la UT no puede depender de ella. Es lo que impide que una sesión larga arrastre suposiciones de otra.

**R4 · Cada UT deja rastro en `PROGRESO.md`.** Ver §5.

**R5 · Un commit por UT**, citando el ID y las secciones de origen.

## 2.2 Por qué la verificación es la columna vertebral

Un agente no puede juzgar su propio trabajo mirándolo. Lo único que distingue "hecho" de "parece hecho" es un comando determinista.

Por eso **el orden dentro de cada etapa pone las pruebas antes que la implementación** siempre que la regla ya esté cerrada en los documentos: primero se escribe la prueba que codifica la regla del `§`, después el código que la hace pasar. Así la regla del documento queda **ejecutable**, no solo escrita.

---

# 3 · Entorno de verificación

Comprobado en esta máquina:

| Herramienta | Estado | Para qué |
|---|---|---|
| **.NET SDK 10.0.302** | ✅ | Backend, pruebas, migraciones |
| **SQL Server LocalDB** (`MSSQLLocalDB`) | ✅ | Base de desarrollo y pruebas de integración |
| **sqlcmd 17** | ✅ | Verificación directa de esquema y semillas |
| **Docker 29.3** | ✅ | SQL Server en contenedor para CI |
| **Android SDK** (platforms 34, 36, 36.1) | ✅ | Compilación de la app |
| **JDK** (JBR de Android Studio) | ✅ | Gradle — hay que fijar `JAVA_HOME` |
| **Emulador + system-images** | ✅ | Verificación visual puntual |

> **El bucle de verificación cierra sin depender de nadie.** Backend y Android se compilan y se prueban aquí. Los teléfonos físicos siguen haciendo falta para lo que no se puede emular — escaneo real de QR, comportamiento con red inestable, entrega de notificación con la app cerrada — pero eso son **puntos de control**, no bloqueos del avance diario.

**Correcciones al TRD derivadas de esto** *(aplicadas)*: se apunta a **.NET 10** en lugar de .NET 8, y `targetSdk 36` con `minSdk 26`.

---

# 4 · Datos simulados

El cliente autorizó simular el perfil de los trabajadores para poder probar, con los datos reales cargándose después.

## 4.1 El principio: la semilla es adversaria, no plausible

> **Una semilla que solo parece realista es peor que ninguna**, porque las pruebas pasan sobre datos que nunca disparan las reglas. Si ningún operario simulado tiene una restricción médica que choque con un puesto real, la regla del §7.2 nunca se ejerce y el sistema parece correcto estando roto.

La semilla se diseña **para provocar cada regla al menos una vez**:

| Escenario sembrado | Regla que ejerce |
|---|---|
| Operario con restricción que choca con su propio puesto habitual | §7.2 + escalera §8.5 nivel 1 |
| Restricción **caducada** ayer | C14 — no debe bloquear |
| Restricción **permanente** (`fecha_fin` nula) | C14 |
| Operador A ausente **con** Operador B disponible | §8.3 suplente |
| Operador A ausente **sin** Operador B | §8.3 vacante crítica |
| Menos Operadores B que puestos fijos descubiertos | Escasez → reparto por prioridad §8.3 |
| Persona que cerró ayer en "Girar botellas" | §7.4 + A4 + B6 |
| Persona que cerró en "Girar botellas" **hace 3 jornadas** | B6 — debe seguir bloqueando |
| Línea exactamente en su piso de seguridad | B5 — inmune a extracción |
| Línea **una persona por encima** del piso | B5 — extraíble una sola vez |
| Puesto con umbral bajo y otro con umbral alto | A4 + B3 — exceso relativo, no minutos |
| Persona en doble turno | §11.5, B7 |
| Persona ausente justificada | §6.1 — nunca asignable |
| Puesto con perfil preferente y candidato sin perfil registrado | §7.3 — no se infiere |
| Personal de liderazgo | §4.1, A7b |
| SKU que desactiva puestos de una línea | §11.2, §5.3 |

## 4.2 Separación de semillas

```
ops/seed/
├── estructural/     10 líneas · prioridad · PROXIMIDAD (A1) · capacidades
│                    ── inmutable, va también a producción ──
├── catalogo/        motivos de excepción, rechazo, paro
│                    ── editable, va a producción ──
└── simulado/        ~160 personas · ~300 puestos · SKU · restricciones
                     ── SOLO desarrollo. Marcado. Nunca a producción ──
```

**Guarda técnica:** las filas simuladas llevan `origen_dato = 'simulado'` y hay una prueba que **falla si la base de producción contiene una sola fila con esa marca**.

## 4.3 El camino de los datos reales

Se construye desde el principio, no se improvisa al final:

- **UT-E3.6** — importador desde CSV/Excel con validación e informe de errores por fila.
- El importador **rechaza el lote entero** si una fila es inválida: cargar medio padrón deja el sistema en un estado peor que vacío.
- **Las restricciones médicas se importan mapeadas al vocabulario de capacidades** *(§7.2)*. Si una fila trae una capacidad que no está en el catálogo, se rechaza — no se crea sobre la marcha.

> ⚠ **Lo simulado desbloquea la construcción, no la puesta en producción.** El vocabulario real de capacidades físicas *(H6)* sigue siendo condición para operar: sin él, las restricciones reales no se pueden mapear y la regla del §7.2 no protege a nadie.

---

# 5 · Continuidad entre sesiones

**El problema específico de ejecutar con un agente:** las sesiones terminan y el contexto se pierde. Sin un mecanismo explícito, cada sesión nueva vuelve a deducir dónde se quedó todo — caro y propenso a error.

**`docs/PROGRESO.md`** es la solución: el estado de las ~95 UTs, con su verificación.

Al empezar cualquier sesión:

```
1. Leer PROGRESO.md          → qué UT toca
2. Leer solo el LEE de esa UT → presupuesto de contexto
3. Ejecutar
4. Verificar en verde
5. Marcar en PROGRESO.md + commit
```

> **Nunca se lee la documentación entera para hacer una UT.** Es lo que mantiene el trabajo dentro de presupuesto y lo que hace que la sesión número 40 sea tan fiable como la primera.

---

# 6 · Puntos de control humano

Momentos donde el avance **se detiene** hasta que el humano valide. Son pocos y deliberados.

| PC | Después de | Qué se valida | Por qué ahí |
|---|---|---|---|
| **PC-1** | E2 · Aislamiento | Dos supervisores, dos teléfonos: ninguno ve la línea del otro | Todo se construye encima. Si falla, se arrastra a todo |
| **PC-2** | E4 · Validación | La restricción médica bloquea por los 8 caminos | Es la regla dura del proyecto |
| **PC-3** | E6 · Llenado | **Un supervisor llena su línea desde un teléfono real** | Primer producto reconocible. Quedan 4 etapas de motores por delante: corregir aquí es barato |
| **PC-4** | E10 · Relevos | El ejemplo del §9.4 completo, con relevo en cadena | El corazón del producto |
| **PC-5** | E13 · Sin conexión | Se corta la red y nada se encola | §12.1 es contraintuitivo; hay que verlo |
| **PC-6** | E14 · Piloto | Un turno real completo, sin papel | Criterio de lanzamiento |

> **PC-3 es el más importante.** Hasta ahí todo lo construido es invisible. Es la primera vez que el producto se puede juzgar de verdad, y quedan por delante las etapas más caras.

---

# 7 · Las etapas

Cada etapa mapea a una fase del [roadmap](06_ROADMAP.md). Aquí se listan las UTs; el detalle completo de cada una vive en `PROGRESO.md`.

## E0 · Entorno *(3 UT)*

| UT | Objetivo | Verifica |
|---|---|---|
| E0.1 | Fijar `JAVA_HOME`, comprobar Gradle y LocalDB | `gradle -v` y `sqlcmd -S (localdb)\MSSQLLocalDB -Q "SELECT 1"` |
| E0.2 | Esqueleto de solución backend según [05 §5](05_TRD.md) | `dotnet build` |
| E0.3 | Esqueleto Android + CI local | `gradlew assembleDebug` |

## E1 · Cimientos y semilla estructural *(6 UT)* → F0

| UT | Objetivo | Verifica |
|---|---|---|
| E1.1 | Migración 001: líneas, puestos, catálogos | Migración aplica y revierte |
| E1.2 | Semilla estructural: 10 líneas, L8 como Bolsón, prioridad base | Consulta de conteo |
| E1.3 | **Semilla `ProximidadLinea` con la corrección A1** | Prueba celda por celda |
| E1.4 | **Prueba: la fila de L10 es `L9,L3,L6,L7,L4,L2,L1,L5,L8`** | Prueba dedicada |
| E1.5 | Semilla de capacidades físicas y catálogos | Conteo |
| E1.6 | Pipeline CI: build, test, arquitectura, secretos | CI en verde |

> **E1.3 y E1.4 parecen triviales y no lo son.** La proximidad es un dato corregido a mano *(A1)*. Si entra mal, el motor de relevos de E9 funcionará **perfectamente enviando gente al sitio equivocado**, y el fallo será invisible hasta que alguien camine de más. Se verifica aquí, no allí.

## E2 · Identidad y aislamiento *(6 UT)* → F1 · `[BLOQUEANTE]`

| UT | Objetivo | Verifica |
|---|---|---|
| E2.1 | `Usuario`, `SesionDispositivo` | Migración |
| E2.2 | JWT + refresh + PIN. **Sin `linea_id` en el token** *(§2.3)* | Pruebas de auth |
| E2.3 | Filtro de rol + filtro de alcance por línea | Pruebas por endpoint |
| E2.4 | **RLS en SQL Server** *([04 §6.3](04_ESQUEMA_BACKEND.md))* | Prueba que salta el filtro de app y sigue bloqueado |
| E2.5 | `Auditoria` + `sp_RegistrarAuditoria`, **incluidos rechazos** | Prueba de traza |
| E2.6 | **Suite de aislamiento** *([05 §6.2](05_TRD.md))* | Suite completa en verde |

→ **PC-1**

## E3 · Personal y puestos *(6 UT)* → F2

| UT | Objetivo | Verifica |
|---|---|---|
| E3.1 | `Puesto` con **umbrales propios** nulables *(A4)*, `titular_id` *(C12)* | Migración + pruebas |
| E3.2 | `PuestoCapacidad`, `TipoActividad` con bandera 24h *(A4)* | Pruebas |
| E3.3 | `Personal` con `perfil` **nulable** *(§7.3)*, `linea_habitual` *(C3)* | Pruebas |
| E3.4 | `RestriccionMedica` **con vigencia, sin borrado** *(C14)* | Prueba: `DELETE` denegado |
| E3.5 | **Semilla simulada adversaria** *(§4.1)* | Prueba: los 16 escenarios existen |
| E3.6 | **Importador de datos reales** con rechazo por lote *(§4.3)* | Pruebas de importación |

## E4 · Motor de validación *(8 UT)* → F3 · `[BLOQUEANTE]`

| UT | Objetivo | Verifica |
|---|---|---|
| E4.1 | `fn_TieneRestriccionBloqueante` — solo vigentes | Pruebas incluida la caducada |
| E4.2 | `fn_CategoriaCompatible` — matriz §4.2 casilla por casilla | Tabla completa |
| E4.3 | `fn_PerfilIncompatible` — NULL **no** aplica la regla | Pruebas |
| E4.4 | `fn_ViolaNoRepeticion24h` — **solo la actividad marcada** *(A4)* | Pruebas incluidos 3 días |
| E4.5 | `fn_VentanaArranqueBloquea` | Pruebas |
| E4.6 | **`sp_ValidarAsignacion`: 7 pasos en orden** *(§7.1)* | Prueba del orden exacto |
| E4.7 | `DENY` a la cuenta de app *([04 §7.5](04_ESQUEMA_BACKEND.md))* | Prueba: `INSERT` directo falla |
| E4.8 | **Suite de reglas de seguridad**: médicas × 8 caminos | Suite en verde |

→ **PC-2**

## E5 · Jornada, prioridad y barrido *(7 UT)* → F4a

| UT | Objetivo |
|---|---|
| E5.1 | `Turno`, **día de operación**, hora del servidor *(C6)* |
| E5.2 | `JornadaLinea`, `PrioridadLinea` versionada *(B8)* |
| E5.3 | `SKU`, `PuestoSKU` → cómputo de *fuera de operación* *(§5.3)* |
| E5.4 | Planificación + rechazo si falta supervisor *(§8.1)* |
| E5.5 | **`sp_BarridoPuestosFijos` por prioridad, solo `tipo='fijo'`** *(§8.3)* |
| E5.6 | `titular_original_id` + vacante crítica + micro-copia §12.5 |
| E5.7 | Ventana de arranque por jornada-línea *(§8.4)* |

## E6 · App base y llenado *(8 UT)* → F4b

| UT | Objetivo |
|---|---|
| E6.1 | Sistema de diseño en Compose: tokens de [03 §2](03_UIUX_BRIEF.md) |
| E6.2 | **Los cuatro estados de pantalla** *(§12.4)* — cargando ≠ vacío |
| E6.3 | Login, PIN, **alta de dispositivo por QR** *(F3)* |
| E6.4 | Malla de línea, una columna, fijos → rotativos |
| E6.5 | Escáner QR con ML Kit **en dispositivo** *(E1, §12.1)* |
| E6.6 | **Modal de confirmación de identidad** *(§12.2)* |
| E6.7 | `sp_SugerirPuesto` — escalera de 4 niveles *(§8.5)* |
| E6.8 | **Concurrencia**: bloqueo determinista + idempotencia *(B1)* |

→ **PC-3** — primer producto reconocible

## E7 · Fatiga *(4 UT)* → F5

| UT | Objetivo |
|---|---|
| E7.1 | Reloj desde `Asignacion.inicio`, **umbral propio** *(A4)* |
| E7.2 | **Exceso relativo en %** — nunca minutos entre puestos *(A4, B3)* |
| E7.3 | Tres niveles; fijos no acumulan; **fatiga del puesto, no de la categoría** *(A7)* |
| E7.4 | Factor de doble turno **default 1.0** *(B7)* + barra continua |

## E8 · Movimiento entre líneas *(6 UT)* → F6

| UT | Objetivo |
|---|---|
| E8.1 | Despacho con `hora_salida` *(§12.7)* |
| E8.2 | **Tránsito inmune** — `UX_Mov_transito` *(§6.1)* |
| E8.3 | Recepción **individual** con `hora_llegada` *(C8)* |
| E8.4 | Rechazo con **motivo obligatorio** *(C10)* |
| E8.5 | Reserva de puesto — `UX_Mov_reserva` *(B4)* |
| E8.6 | `sp_CaducarTransitos`: **alerta y no mueve a nadie** *(B11)* |

## E9 · Motor de relevos *(9 UT)* → F7 · la etapa más grande

| UT | Objetivo |
|---|---|
| E9.1 | `SolicitudRelevo`; **el puesto no se libera al avisar** *(§9.4 p1)* |
| E9.2 | Orden de cola: crítico → exceso relativo → FIFO *(B3)* |
| E9.3 | **Aviso a todos los supervisores SIN identidad** *(D2)* |
| E9.4 | `vw_SolicitudRelevo_L8` — proyección mínima *(D1)* |
| E9.5 | `sp_ProponerRelevista` con el ranking exacto *(B2)* |
| E9.6 | Aceptar / rechazar + descartados con caducidad *(B10)* |
| E9.7 | **`sp_SugerirDestinoRelevado`: línea → proximidad → L8** *(B4, A1, A9)* |
| E9.8 | **Prueba del ejemplo normativo del §9.4** *(A8)* |
| E9.9 | **Prueba de arquitectura: Relevos no referencia prioridad** *(A9)* |

→ **PC-4**

## E10 · Extracción inversa y vacante crítica *(6 UT)* → F8

| UT | Objetivo |
|---|---|
| E10.1 | Orden **derivado** invirtiendo la prioridad *(A5)* |
| E10.2 | Solo con la L8 completamente vacía *(§9.6)* |
| E10.3 | Piso por línea; en el mínimo = inmune *(B5)* |
| E10.4 | Escalera C15 N1→N4 + **guarda anti-dominó** *(C15)* |
| E10.5 | `JustificacionExcepcion` en toda excepción *(A6)* |
| E10.6 | Titular reincorporado *(C1)* + salida de retiro temporal *(C2)* |

⚠ Requiere **H7** cerrado: A5b y A7-orig.

## E11 · Contingencias y estadística *(8 UT)* → F9

| UT | Objetivo |
|---|---|
| E11.1 | Paro: dos niveles + **descripción obligatoria** *(§11.1)* |
| E11.2 | Fijos ocupados, rotativos liberados con tránsito individual |
| E11.3 | **Cronómetro persistente en todas las pantallas** *(§11.1)* |
| E11.4 | Relevista en tránsito hacia línea en paro *(C9)* |
| E11.5 | `Lote`, cambio de SKU *(C5, §11.2)* |
| E11.6 | Desperdicio + producción + justificación sobre umbral |
| E11.7 | **Eficiencia calculada en el servidor** *(§11.4, C4)* |
| E11.8 | **Todo registro empuja a los dos paneles** *(C4)* |

## E12 · Tiempo real y notificaciones *(6 UT)* → F10

| UT | Objetivo |
|---|---|
| E12.1 | Hub SignalR, **grupos asignados por el servidor** *(§2.2)* |
| E12.2 | Catálogo de eventos *([05 §2.4](05_TRD.md))* |
| E12.3 | Bandeja de salida transaccional |
| E12.4 | **FCM campana vacía** + descarga del contenido real *(D5)* |
| E12.5 | **Prueba: falla el build si la carga útil lleva negocio** *(D5)* |
| E12.6 | Acuse, escalado, *"supervisor no localizable"* |

⚠ Requiere **H3**.

## E13 · Modo sin conexión *(5 UT)* → F11

| UT | Objetivo |
|---|---|
| E13.1 | Room + SQLCipher, clave en Keystore *(D3)* |
| E13.2 | **Caché acotada a su línea** — nunca el padrón *(D3)* |
| E13.3 | **Bloqueo defensivo: no se encola nada** *(§12.1)* |
| E13.4 | Sello de frescura + degradación visual *(D4)* |
| E13.5 | Detección por **latido**, no por estado del adaptador |

→ **PC-5**

## E14 · Cierre, histórico y endurecimiento *(7 UT)* → F12

| UT | Objetivo |
|---|---|
| E14.1 | Cierre de turno con **lista exacta de bloqueos** *(C13)* |
| E14.2 | `UltimaTareaJornada` al cerrar *(B6)* + cierre forzado *(A6)* |
| E14.3 | Histórico y auditoría consultable |
| E14.4 | Rendimiento contra presupuestos *([05 §3.4](05_TRD.md))* |
| E14.5 | Accesibilidad: 48 dp, AAA, **escala de grises** *(A11)* |
| E14.6 | Distribución del APK + verificación de versión *(F3)* |
| E14.7 | Carga de datos reales *(H5, H6)* + purga de lo simulado |

→ **PC-6 · piloto**

---

# 8 · Resumen

| Etapa | UTs | Fase | Control |
|---|---|---|---|
| E0 Entorno | 3 | — | |
| E1 Cimientos | 6 | F0 | |
| E2 Aislamiento | 6 | F1 | **PC-1** |
| E3 Personal y puestos | 6 | F2 | |
| E4 Validación | 8 | F3 | **PC-2** |
| E5 Jornada y barrido | 7 | F4a | |
| E6 App y llenado | 8 | F4b | **PC-3** |
| E7 Fatiga | 4 | F5 | |
| E8 Movimiento | 6 | F6 | |
| E9 Relevos | 9 | F7 | **PC-4** |
| E10 Extracción y C15 | 6 | F8 | |
| E11 Contingencias | 8 | F9 | |
| E12 Tiempo real | 6 | F10 | |
| E13 Sin conexión | 5 | F11 | **PC-5** |
| E14 Cierre y piloto | 7 | F12 | **PC-6** |
| **Total** | **95** | | **6 controles** |

---

# 9 · Riesgos de este modelo de ejecución

| Riesgo | Mitigación |
|---|---|
| **La semilla simulada no ejerce las reglas** | §4.1: la semilla es adversaria por diseño, con prueba que verifica los 16 escenarios |
| **Se inventa un valor de negocio** | Cláusula R2 en toda UT de motor + los parámetros se siembran vacíos |
| **Se pierde el hilo entre sesiones** | `PROGRESO.md` + presupuesto de contexto por UT |
| **Una UT se da por hecha sin verificar** | R1: verde o no termina |
| **Se avanza sin validación humana hasta el final** | 6 puntos de control, el más importante en E6 |
| **Los motores se fusionan en un refactor** | E9.9: prueba de arquitectura que falla la compilación *(A9)* |
| **Lo simulado llega a producción** | `origen_dato = 'simulado'` + prueba que falla si aparece en producción |
| **Se implementa cola offline "para mejorar la experiencia"** | §12.1 la prohíbe; antipatrón documentado en [06 §3.3](06_ROADMAP.md) |

---

# 10 · Arranque

**Sin bloqueos.** El entorno está verificado *(§3)* y los datos se simulan *(§4)*. Se empieza por **UT-E0.1** y se avanza hasta el primer punto de control.

Lo único que conviene arrancar en paralelo, porque no depende del desarrollo y tiene plazo propio:

- **H4** — diseño e impresión de los gafetes con QR.
- **H6** — sesión con Enfermería para el vocabulario de capacidades físicas.

Y antes de llegar a E10: **H7**, cerrar A5b y A7-orig.
