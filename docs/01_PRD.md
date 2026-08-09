# SmartAssign — Product Requirement Document

**Qué construimos, para quién, y cómo sabremos que funcionó.**
Versión 1.0 · 2026-08-08

> **Relación con las fuentes.** La [Especificación Funcional v3.3](fuentes/ESPECIFICACION_FUNCIONAL_SMARTASSIGN.md) es el contenido de negocio de este documento. Este PRD **no lo reescribe ni lo reinterpreta**: le da forma de PRD y añade lo que le faltaba — alcance, métricas y criterios de aceptación. Toda regla ambigua está resuelta en el [Registro de Decisiones](00_DECISIONES.md) y se cita por su identificador.
>
> Cada requisito lleva su referencia de origen: `§x.y` a la especificación, `A1`–`E7` al registro de decisiones.

---

# 1 · Propósito y visión de negocio

## 1.1 El problema

Una planta industrial reparte cada día unos 160 trabajadores entre 10 líneas de producción. Hoy ese reparto se hace de palabra y en papel, y produce cinco pérdidas concretas *(§1.1)*:

| Pérdida | Manifestación en planta |
|---|---|
| **Nadie sabe dónde está la gente** | Entre que un trabajador sale de una línea y llega a otra desaparece del control. Dos supervisores cuentan con la misma persona a la vez. |
| **Se asigna gente a puestos que no debería ocupar** | Enfermería dicta restricciones médicas, pero esa información vive en una carpeta, no en el momento en que el supervisor decide. |
| **El desgaste no se controla** | Alguien pasa cuatro horas seguidas en una tarea repetitiva, o la repite dos días seguidos, sin que nada lo señale. |
| **Se pierde tiempo productivo** | Cuando una línea se detiene por avería, su personal queda ocioso porque nadie tiene la visión de conjunto para reubicarlo. |
| **No hay memoria** | Cuánto se paró, por qué, cuánto material se desperdició, qué tan eficiente fue el turno: todo se estima al final del día. |

## 1.2 Propuesta de valor

SmartAssign convierte el reparto de personal en un proceso **controlado, trazable y verificable** *(§1.2)*:

- En todo momento se sabe **dónde está cada persona** y en qué situación.
- Las **reglas de seguridad ocupacional se aplican solas**, en el instante de la decisión.
- El **desgaste es visible** antes de ser un problema.
- El **tiempo muerto se reduce** reubicando personal en cuanto una línea se detiene.
- Cada turno deja un **registro medible**.

## 1.3 El principio de producto que gobierna todo

> **El sistema nunca miente sobre lo que está pasando** *(§1.3)*.

Si algo no se puede hacer, dice **por qué** y **qué hacer para desbloquearlo**, en lenguaje de planta.

**Este principio no es decorativo: es un requisito funcional con consecuencias verificables.** Un sistema que rechaza sin explicar entrena al supervisor a ignorarlo y a resolver por fuera — y ahí deja de servir. Por eso, en este producto:

- **el rechazo es funcionalidad**, no un caso de error. Cada rechazo tiene su criterio de aceptación propio en este documento;
- **"cargando" y "vacío" nunca se ven igual** *(§12.4)*;
- **ningún indicador se muestra sin sello de frescura** *(D4)*.

## 1.4 Qué NO es SmartAssign

Delimitarlo evita expectativas que el producto no cumple y que ninguna fuente pidió:

- **No es un sistema que mueva personal por su cuenta.** El sistema señala, sugiere y advierte; el supervisor decide y ejecuta *(§9.3)*.
- **No es un sistema de asistencia.** No sustituye al reloj checador *(C3)*.
- **No es un MES.** No mide la máquina; registra lo que el supervisor observa *(C4)*.
- **No es un expediente médico.** Almacena las capacidades físicas prohibidas que Enfermería dicta, no diagnósticos *(§7.2, C14)*.

---

# 2 · Usuarios

El sistema tiene **exactamente dos roles**, con alcances opuestos *(Parte II)*.

## 2.1 Coordinador — visión global

| | |
|---|---|
| **Cuántos** | Uno por planta |
| **Alcance** | Las 10 líneas |
| **Contexto de uso** | Oficina y planta. Puede sentarse a planificar. |
| **Qué hace** | Planifica la jornada, mantiene el padrón, arranca el turno, interviene sobre cualquier línea, consulta el histórico |
| **Su momento crítico** | La planificación del día anterior y el gatillo de arranque: si se equivoca ahí, el turno entero arranca mal |
| **Su poder exclusivo** | Es el único que puede cruzar el aislamiento entre líneas *(§2.1.8)* y el único que puede ejecutar excepciones — siempre con justificación *(A6)* |

## 2.2 Supervisor — una sola línea

| | |
|---|---|
| **Cuántos** | Uno por línea activa |
| **Alcance** | **Exclusivamente su línea** |
| **Contexto de uso** | De pie, con guantes, con una mano, moviéndose por la planta, bajo iluminación industrial variable *(§12.3)* |
| **Qué hace** | Llena sus puestos rotativos, vigila fatiga, pide y recibe relevos, registra paros y desperdicio, cierra su turno |
| **Su momento crítico** | El arranque: recoger a su grupo y colocarlo antes de que la ventana se cierre *(§8.4)* |
| **Lo que nunca puede** | Ver ni tocar puestos ni personal de otras líneas. **El aislamiento es total y deliberado** *(§2.2)* |

### 2.2.1 El Supervisor de la L8 — misma cuenta, otra pantalla

No es un tercer rol. Es un Supervisor cuya línea asignada es la L8, y eso le cambia dos cosas *(C7, §2.2.7)*:

- **Su pantalla principal es distinta:** no una malla de puestos, sino personal disponible + cola de relevos + recepciones pendientes.
- **Tiene una capacidad que ningún otro supervisor tiene:** aceptar o rechazar propuestas de relevista para puestos de otras líneas.

De la línea ajena ve **el puesto, nunca a la persona** *(D1)*.

## 2.3 Regla de supervisor único

> **Cada línea activa tiene un supervisor, y cada supervisor tiene una línea** *(§2.3)*.

La línea **la determina el sistema** según la asignación del Coordinador. **Nunca la elige el supervisor de una lista.** Si no tiene línea asignada, no puede operar, y el sistema debe decírselo con claridad y remitirlo al Coordinador.

---

# 3 · Alcance

## 3.1 Objetivos del lanzamiento (Release Goals)

El MVP se considera lanzable cuando un turno completo se puede operar de principio a fin sin papel. Concretamente, estos cinco objetivos:

| # | Release Goal | Se verifica con |
|---|---|---|
| **RG-1** | Un turno completo se opera íntegramente en la app: planificación, arranque, llenado, relevos, contingencias y cierre | Un turno piloto real, sin registro paralelo en papel |
| **RG-2** | Ninguna asignación viola una restricción médica, en ningún camino del sistema | Suite de pruebas que recorre los 4 motores × las 7 validaciones + auditoría del turno piloto |
| **RG-3** | Ningún supervisor accede a datos de personal de otra línea | Pruebas de autorización sobre cada endpoint + revisión de la traza de auditoría |
| **RG-4** | En todo momento se sabe dónde está cada persona, incluido el trayecto | Cero personas en estado indeterminado al cierre del turno piloto |
| **RG-5** | El turno deja un registro medible: eficiencia, paros, desperdicio, tiempos de traslado | Informe de cierre generado sin intervención manual |

## 3.2 Dentro del alcance del MVP

### Coordinador *(§2.1)*

| ID | Capacidad | Origen |
|---|---|---|
| CO-01 | Planificar la jornada: líneas activas, SKU por línea, turnos cubiertos | §2.1.1, §8.1 |
| CO-02 | Asignar un supervisor a cada línea activa | §2.1.2 |
| CO-03 | Definir y reordenar la prioridad de líneas en caliente | §2.1.3, §3.3, B8 |
| CO-04 | Gatillar el arranque del turno | §2.1.4, §8.3 |
| CO-05 | Ver el estado de las 10 líneas en tiempo real | §2.1.5, C4 |
| CO-06 | Gestionar el padrón: alta, edición, baja, reactivación, habilidades, disponibilidad, ausencias, restricciones médicas, doble turno | §2.1.6, C14, B7 |
| CO-07 | Gestionar supervisores: alta, baja, reasignación de línea | §2.1.7 |
| CO-08 | Intervenir sobre cualquier línea sin restricción de línea propia | §2.1.8 |
| CO-09 | Mover personal al margen de fatiga, con justificación obligatoria | §2.1.9, A6 |
| CO-10 | Acceder y editar todos los datos maestros desde la interfaz | §2.1.10 |
| CO-11 | Consultar el histórico: jornadas, paros, desperdicio, eficiencia | §2.1.11 |
| CO-12 | Reincorporar a alguien desde retiro temporal | C2 |
| CO-13 | Ejecutar extracción de Operador B por vacante crítica desde otra línea | C15-N3 |
| CO-14 | Cancelar un tránsito caducado | B11 |
| CO-15 | Forzar el cierre de turno de una línea, con justificación | C13, A6 |

### Supervisor *(§2.2)*

| ID | Capacidad | Origen |
|---|---|---|
| SU-01 | Ver la malla de puestos de su línea con ocupación, situación y fatiga | §2.2.1 |
| SU-02 | Registrar operarios en puestos rotativos por escaneo o búsqueda | §2.2.2 |
| SU-03 | Confirmar identidad antes de consolidar | §2.2.3, §12.2 |
| SU-04 | Liberar a una persona hacia la L8 | §2.2.4, §9.7 |
| SU-05 | Registrar retiro temporal | §2.2.5, §9.7 |
| SU-06 | Solicitar relevo sin liberar el puesto | §2.2.6, §9.4 |
| SU-07 | *(Solo L8)* Aceptar o rechazar la propuesta de relevista | §2.2.7 |
| SU-08 | Recibir al relevista y despachar al relevado | §2.2.8, §9.4 |
| SU-09 | Consultar restricciones médicas de su personal | §2.2.9 |
| SU-10 | Registrar paros técnicos y reanudar producción | §2.2.10, §11.1 |
| SU-11 | Registrar desperdicio y producción al cierre de lote | §2.2.11, §11.3, C4, C5 |
| SU-12 | Cerrar el turno de su línea | §2.2.12, C13 |
| SU-13 | Devolver el puesto a un titular reincorporado | C1 |
| SU-14 | Cubrir una vacante crítica de puesto fijo con un Operador B de su línea | C15-N2 |

### Motores del sistema

| ID | Motor | Origen |
|---|---|---|
| MO-01 | Asignación inicial: barrido de puestos fijos por jerarquía de prioridad | §8.3, A9 |
| MO-02 | Escalera de sugerencia de puesto | §8.5 |
| MO-03 | Relevos: detección de fatiga, propuesta, tránsito, reasignación en cadena | §9.1–§9.5, A1, A9, B2, B3, B4 |
| MO-04 | Extracción inversa con piso de seguridad | §9.6, A5, B5 |
| MO-05 | Validación central de las 7 reglas | §7, B12 |
| MO-06 | Cálculo y difusión de estadística en vivo | §11.4, C4 |
| MO-07 | Auditoría y trazabilidad de todo movimiento | §12.7 |

## 3.3 Fuera de alcance (Out of Scope)

Explícito, para que nadie lo espere:

| Fuera de alcance | Por qué | Ref. |
|---|---|---|
| Integración con reloj checador / sistema de asistencia | §6.1 permite asignar a quien está *fuera de turno*: el marcaje nunca fue requisito | C3 |
| Integración MES / SCADA para producción automática | La producción la captura el supervisor; ningún documento pidió integración | C4 |
| Rol de Enfermería como usuario | §2.1.6 pone el registro médico bajo el Coordinador | C14 |
| SKU, eficiencia, desperdicio y paros de la L8 | La fórmula del §11.4 exige un ritmo teórico de SKU que la L8 no tiene | C7 |
| Multi-planta | Toda la especificación describe una planta de 10 líneas | — |
| Cliente iOS o web | El anexo prescribe Android nativo | Anexo §1 |
| Envío de datos de personal a cualquier servicio de terceros | **Prohibido explícitamente** | §12.1 |
| Cola optimista de operaciones offline | §12.1 exige bloqueo defensivo, no encolado | §12.1 |
| Nómina, costes, productividad individual | Ninguna fuente lo menciona | — |

---

# 4 · Historias de usuario y criterios de aceptación

Formato: **Como** [rol], **quiero** [acción] **para** [beneficio]. Criterios en Gherkin.

> **Convención de este documento:** cada historia incluye al menos un escenario de **rechazo**. En SmartAssign el rechazo no es un caso de error: es la funcionalidad que sostiene el §1.3 y las reglas duras de seguridad.

## 4.1 Épica A — Planificación y arranque

### HU-A1 · Planificar la jornada
> **Como** Coordinador, **quiero** definir qué líneas operan mañana y con qué SKU, **para** que el sistema sepa qué puestos existen y quién sobra. *(§8.1)*

```gherkin
Escenario: Línea con SKU planificado queda activa
  Dado que planifico la jornada del día siguiente
  Cuando asigno un SKU a la L4
  Entonces la L4 queda "activa"
  Y sus puestos requeridos por ese SKU quedan "libres"
  Y los puestos no requeridos quedan "fuera de operación"

Escenario: Línea sin SKU queda inactiva y su gente se absorbe
  Dado que no asigno SKU a la L6
  Cuando confirmo la planificación
  Entonces la L6 queda "inactiva"
  Y todos sus puestos quedan "fuera de operación"
  Y el personal habitual de la L6 que se presente será absorbido por la L8

Escenario: Rechazo por línea activa sin supervisor
  Dado que la L4 tiene SKU asignado y ningún supervisor
  Cuando intento confirmar la planificación
  Entonces el sistema rechaza la confirmación
  Y nombra exactamente qué líneas activas están sin supervisor
```

### HU-A2 · Asignar supervisor a cada línea
> **Como** Coordinador, **quiero** asignar un supervisor a cada línea activa, **para** que cada línea tenga un responsable y ningún supervisor elija su línea. *(§2.2, §2.3)*

```gherkin
Escenario: El supervisor no elige su línea
  Dado que soy Supervisor y tengo la L7 asignada
  Cuando entro a la aplicación
  Entonces veo la L7 sin haberla seleccionado
  Y no existe ninguna forma de cambiar de línea

Escenario: Supervisor sin línea asignada
  Dado que soy Supervisor y no tengo línea asignada
  Cuando entro a la aplicación
  Entonces el sistema me lo dice con claridad
  Y me remite al Coordinador
  Y no puedo ejecutar ninguna operación

Escenario: Rechazo por doble asignación
  Dado que la L2 ya tiene supervisor
  Cuando intento asignar un segundo supervisor a la L2
  Entonces el sistema lo rechaza nombrando al supervisor actual
```

### HU-A3 · Reordenar la prioridad en caliente
> **Como** Coordinador, **quiero** reordenar la prioridad de las líneas durante la jornada, **para** responder a un pedido urgente o a una alerta de calidad sin esperar al día siguiente. *(§2.1.3, §3.3, B8)*

```gherkin
Escenario: El cambio aplica solo hacia adelante
  Dado que hay asignaciones ya ejecutadas con la prioridad anterior
  Cuando reordeno la prioridad a mitad de turno
  Entonces ninguna asignación vigente se modifica
  Y ninguna persona recibe orden de moverse
  Y el nuevo orden rige la siguiente corrida de asignación inicial
  Y rige el orden de extracción inversa desde ese instante
  Y el cambio queda auditado con mi identidad, la hora, el valor anterior y el nuevo
```

### HU-A4 · Gatillar el arranque del turno
> **Como** Coordinador, **quiero** gatillar el arranque, **para** que el motor cubra automáticamente todos los puestos fijos y el supervisor solo se ocupe de los rotativos. *(§2.1.4, §8.3)*

```gherkin
Escenario: El barrido recorre las líneas por prioridad
  Dado que las líneas activas tienen prioridad L4 > L1 > L2 > L6 > L7 > L5 > L3 > L8 > L9 > L10
  Cuando gatillo el arranque
  Entonces el motor procesa primero la L4, después la L1, y así sucesivamente
  Y los Operadores B se reparten en ese orden

Escenario: Titular presente
  Dado un puesto fijo cuyo titular está presente
  Cuando corre el barrido
  Entonces se le asigna automáticamente
  Y el puesto muestra "Asignado automáticamente por asistencia"

Escenario: Titular ausente, cubierto por suplente
  Dado un puesto fijo cuyo titular está ausente
  Y hay un Operador B disponible y compatible
  Cuando corre el barrido
  Entonces se asigna al Operador B
  Y el puesto conserva registrada la identidad del titular original
  Y muestra "Cubierto por suplente — titular ausente"

Escenario: Vacante crítica
  Dado un puesto fijo sin titular presente y sin Operador B disponible
  Cuando corre el barrido
  Entonces el puesto queda "vacante crítica"
  Y muestra "Sin titular ni suplente disponible"
  Y se destaca por encima de las vacantes normales

Escenario: Los rotativos no se tocan
  Cuando corre el barrido
  Entonces ningún puesto rotativo queda asignado
  Y todos los rotativos de líneas activas quedan "libres"
```

## 4.2 Épica B — Llenado de línea

### HU-B1 · Registrar un operario en un puesto rotativo
> **Como** Supervisor, **quiero** registrar a un operario escaneando su gafete, **para** colocar a mi gente rápido sin escribir con guantes. *(§2.2.2, §12.2)*

```gherkin
Escenario: Registro con confirmación de identidad
  Dado que escaneo el gafete de un operario
  Cuando el sistema lo resuelve
  Entonces me muestra nombre completo, número de ficha, categoría
  Y sus restricciones médicas activas de forma explícita
  Y la asignación no se consolida hasta que confirmo deliberadamente

Escenario: El escaneo por sí solo nunca asienta
  Dado que escaneo un gafete
  Cuando no confirmo
  Entonces no se registra ninguna asignación

Escenario: Búsqueda manual para gafete ilegible
  Dado que el gafete está dañado
  Cuando busco por nombre o ficha
  Entonces solo veo personal disponible
  Y aparecen primero quienes están físicamente en mi línea

Escenario: Resultado comunicado con texto y forma
  Cuando una validación se resuelve
  Entonces el resultado se comunica con texto y forma
  Y nunca solo mediante color
```

### HU-B2 · Recibir la sugerencia de puesto
> **Como** Supervisor, **quiero** que el sistema me proponga a qué puesto va cada persona, **para** no tener que recordar el puesto habitual de 16 operarios. *(§8.5)*

```gherkin
Esquema del escenario: Escalera de sugerencia
  Dado que registro a una persona sin haber seleccionado puesto
  Cuando el motor busca destino
  Entonces propone <resultado>

  Ejemplos:
    | situación                                              | resultado                              |
    | su puesto de titular está libre y cumple todo          | ese puesto                             |
    | su puesto de titular cumple todo salvo perfil          | ese puesto, indicando que cede perfil  |
    | hay otro puesto compatible que cumple todo             | ese puesto                             |
    | hay otro compatible que cumple todo salvo perfil       | ese puesto, indicando que cede perfil  |
    | nada aplica                                            | nada, y nombra la regla que lo impidió |

Escenario: Las médicas no ceden en ningún nivel
  Dado que la persona tiene una restricción médica activa
  Y todos los puestos libres exigen esa capacidad
  Cuando el motor recorre los cuatro niveles
  Entonces no propone ningún puesto en ningún nivel
  Y nombra la restricción médica como causa
```

### HU-B3 · Operar dentro de la ventana de arranque
> **Como** Supervisor, **quiero** que durante los primeros minutos solo se admita a quien está en mi línea, **para** que el arranque no se convierta en gente cruzándose por la planta. *(§8.4)*

```gherkin
Escenario: Solo personal físicamente presente
  Dado que la ventana de arranque está activa
  Cuando intento registrar a alguien que está físicamente en otra línea
  Entonces el sistema lo rechaza
  Y explica que la ventana de arranque está activa
  Y dice cuánto falta para que termine

Escenario: Movimientos entre líneas bloqueados
  Dado que la ventana está activa
  Cuando intento despachar a alguien a otra línea
  Entonces el sistema lo rechaza con la misma explicación

Escenario: Sin desvíos automáticos
  Dado que la ventana está activa
  Cuando registro a alguien en mi línea
  Entonces el sistema no lo desvía a ninguna línea de mayor prioridad

Escenario: Excepción del Coordinador con justificación
  Dado que soy Coordinador y la ventana está activa
  Cuando ejecuto un movimiento entre líneas
  Entonces el sistema exige el formulario de justificación
  Y sin él la operación no se ejecuta
```

## 4.3 Épica C — Reglas de validación

### HU-C1 · Que la restricción médica bloquee siempre
> **Como** responsable de seguridad ocupacional, **quiero** que una restricción médica deniegue la asignación en cualquier camino del sistema, **para** que nadie termine en un puesto que su condición prohíbe. *(§7.2)* `[REGLA DURA]`

```gherkin
Escenario: Bloqueo en todos los caminos
  Dado que una persona tiene prohibida una capacidad física
  Y un puesto exige esa capacidad
  Cuando se intenta asignarla por cualquier vía
    | vía                                        |
    | barrido automático de puestos fijos        |
    | escalera de sugerencia, los cuatro niveles |
    | registro manual del supervisor             |
    | propuesta de relevista de la L8            |
    | reasignación del relevado                  |
    | extracción inversa                         |
    | intervención del Coordinador               |
    | asignación manual de personal de liderazgo |
  Entonces la asignación se deniega
  Y el sistema nombra la restricción médica como causa

Escenario: La verificación es general
  Dado que una persona tiene varias restricciones registradas
  Cuando se evalúa un puesto
  Entonces se comprueban todas
  Y no solo un tipo concreto de esfuerzo

Escenario: Solo cuentan las restricciones vigentes
  Dado que una restricción tiene fecha de fin anterior a hoy
  Cuando se evalúa la asignación
  Entonces esa restricción no se aplica
  Y sigue constando en el historial
```

### HU-C2 · Que el primer rechazo detenga y explique
> **Como** Supervisor, **quiero** que cuando algo se rechaza se me diga exactamente qué falló, **para** poder resolverlo en el momento en vez de resolverlo por fuera. *(§7.1, §1.3, §12.4)*

```gherkin
Escenario: Orden de evaluación
  Cuando se valida una asignación
  Entonces se evalúa en este orden
    | 1 | ¿El puesto sigue libre?                        |
    | 2 | ¿La persona sigue disponible?                  |
    | 3 | ¿Su categoría es compatible?                   |
    | 4 | ¿Sus restricciones médicas lo permiten?        |
    | 5 | ¿El perfil requerido lo permite?               |
    | 6 | ¿No repitió esta misma tarea ayer?             |
    | 7 | ¿La ventana de arranque lo permite?            |
  Y el primer rechazo detiene el proceso

Escenario: El mensaje es de planta
  Cuando una asignación se rechaza
  Entonces el mensaje nombra la causa concreta
  Y dice cuál es el siguiente paso
  Y no contiene códigos de error ni texto genérico
```

### HU-C3 · Que la concurrencia tenga un solo ganador
> **Como** Supervisor, **quiero** que si otro supervisor captura a la misma persona un instante antes yo reciba un rechazo claro, **para** que nunca dos creamos que la tenemos. *(§7.5, B1)*

```gherkin
Escenario: Gana quien confirma primero en el servidor
  Dado que dos supervisores registran a la misma persona casi a la vez
  Cuando ambas peticiones llegan al servidor
  Entonces la primera que confirma la transacción gana
  Y la segunda recibe: "[Nombre] acaba de ser registrado en [línea] · [puesto] por otro supervisor"

Escenario: Atomicidad
  Cuando una operación de asignación falla en cualquier punto
  Entonces no queda el puesto ocupado con la persona libre
  Ni la persona asignada con el puesto libre
```

### HU-C4 · Que la regla de 24 horas proteja el puesto desgastante
> **Como** responsable de seguridad ocupacional, **quiero** que nadie repita "Girar botellas" dos jornadas seguidas, **para** que la tarea más desgastante no recaiga siempre en la misma persona. *(§7.4, A4, B6)*

```gherkin
Escenario: Bloqueo por jornada anterior
  Dado que una persona ocupó "Girar botellas" al cerrar su jornada anterior
  Cuando se intenta asignarla hoy a "Girar botellas"
  Entonces se deniega nombrando la regla

Escenario: La regla no se extiende a otros puestos
  Dado que una persona ocupó otro puesto rotativo en su jornada anterior
  Cuando se intenta asignarla hoy a ese mismo puesto
  Entonces se permite

Escenario: Los días de descanso no la limpian
  Dado que su jornada anterior fue hace tres días en "Girar botellas"
  Cuando se intenta asignarla hoy a "Girar botellas"
  Entonces se deniega igualmente
```

## 4.4 Épica D — Fatiga y relevos

### HU-D1 · Ver la fatiga de forma continua
> **Como** Supervisor, **quiero** ver cómo avanza la fatiga de cada puesto, no solo cuando cruza el límite, **para** anticiparme en vez de reaccionar. *(§9.1)*

```gherkin
Escenario: Avance continuo
  Cuando miro un puesto rotativo ocupado
  Entonces veo su avance hacia el umbral de forma continua
  Y no solo una alerta al cruzarlo

Escenario: Umbral propio de cada puesto
  Dado que dos puestos tienen umbrales distintos
  Cuando ambos llevan 70 minutos ocupados
  Entonces cada uno muestra su nivel según su propio umbral

Escenario: Los puestos fijos no acumulan
  Cuando miro un puesto fijo ocupado
  Entonces no muestra nivel de fatiga

Escenario: La fatiga es del puesto, no de la categoría
  Dado que un Operador B ocupa un puesto rotativo
  Cuando pasa el tiempo
  Entonces acumula fatiga igual que un operario
```

### HU-D2 · Enterarme de la fatiga en toda la planta sin ver a nadie ajeno
> **Como** Supervisor, **quiero** saber qué está pasando en las otras líneas, **para** entender el contexto — sin acceder a personal que no es mío. *(§9.4 paso 1, §2.2, D2)*

```gherkin
Escenario: Contenido del aviso
  Dado que un puesto de la L4 alcanza "relevo sugerido"
  Cuando recibo el aviso siendo supervisor de la L7
  Entonces veo "L4 · Puesto 3 — relevo sugerido · 62 min"
  Y no veo el nombre, la ficha ni ningún dato de la persona
  Y al abrir el aviso tampoco obtengo más detalle

Escenario: El puesto no se libera al avisar
  Cuando un puesto alcanza el umbral
  Entonces el operario sigue produciendo
  Y el puesto no queda libre hasta que llegue su reemplazo
```

### HU-D3 · Proponer y despachar un relevista *(L8)*
> **Como** Supervisor de la L8, **quiero** que el sistema me proponga el mejor candidato de mi gente para cada puesto fatigado, **para** decidir rápido sin revisar restricciones una por una. *(§9.4 pasos 2 y 3, B2, B3, D1)*

```gherkin
Escenario: Orden de la cola
  Cuando abro la cola de relevos pendientes
  Entonces veo primero los de nivel crítico
  Y dentro del mismo nivel, mayor exceso relativo sobre su propio umbral primero
  Y a igualdad, el más antiguo primero
  Y una vacante crítica de puesto fijo encabeza la cola por delante de cualquier fatiga

Escenario: Solo veo el puesto, nunca a la persona ajena
  Cuando abro una solicitud de la L4
  Entonces veo línea, puesto, tipo, nivel de fatiga y capacidades que exige
  Y no veo el nombre ni las restricciones médicas del operario a relevar

Escenario: Aceptar
  Cuando acepto la propuesta
  Entonces el candidato queda "en tránsito" hacia la línea destino
  Y el puesto fatigado queda reservado para él
  Y el supervisor destino recibe aviso de que viene esa persona a ese puesto concreto

Escenario: Rechazar
  Cuando rechazo la propuesta
  Entonces el candidato queda registrado como descartado para ese puesto concreto
  Y el sistema carga otra sugerencia si existe
  Y si no existe, lo dice explícitamente

Escenario: La lista de descartados no se vuelve un veto invisible
  Dado que rechacé a tres candidatos para un puesto
  Entonces la lista es visible con su conteo
  Y puedo limpiarla
  Y caduca automáticamente al cierre de turno
```

### HU-D4 · Recibir al relevista y reubicar al relevado
> **Como** Supervisor, **quiero** confirmar la llegada del relevista y que el sistema me diga a dónde mando al relevado, **para** que nadie quede parado preguntando. *(§9.4 pasos 5 y 6, B4, A1)*

```gherkin
Escenario: Confirmación de llegada física
  Dado que hay un relevista en tránsito hacia mi línea
  Cuando confirmo que llegó físicamente
  Entonces se le asigna al puesto fatigado reservado
  Y el sistema me presenta de inmediato el destino sugerido para el relevado

Escenario: Destino en la propia línea
  Dado que hay otro puesto fatigado compatible en mi línea
  Entonces el sistema lo sugiere como destino
  Y lo asigno directamente, sin despacho ni tránsito

Escenario: Destino en otra línea
  Dado que no hay ningún puesto fatigado compatible en mi línea
  Entonces el sistema recorre la jerarquía de proximidad de mi línea
  Y sugiere el puesto fatigado de la primera línea que tenga uno compatible
  Y el movimiento se ejecuta bajo despacho, tránsito y recepción

Escenario: Sin destino en todo el recorrido
  Dado que no hay ningún puesto fatigado compatible en ninguna línea del recorrido
  Entonces el destino sugerido es la L8

Escenario: No se propone un puesto ya reservado
  Dado que un puesto fatigado está reservado para otro relevista en tránsito
  Entonces el sistema no lo sugiere como destino del relevado

Escenario: Rechazo de recepción
  Cuando rechazo la recepción
  Entonces el sistema me exige un motivo
  Y la persona queda en tránsito hacia la L8
  Y el puesto vuelve a la cola con su nivel de fatiga actual
  Y la persona queda descartada para ese puesto
```

### HU-D5 · Que la rotación siga siendo decisión humana
> **Como** Supervisor, **quiero** que el sistema avise pero no mueva a nadie por su cuenta, **para** poder aplicar el contexto que el sistema no ve. *(§9.3)*

```gherkin
Escenario: El sistema nunca ejecuta por su cuenta
  Cuando un puesto alcanza cualquier nivel de fatiga
  Entonces el sistema avisa y sugiere
  Y no reasigna a nadie sin una acción humana explícita

Escenario: Escalado del crítico sostenido
  Dado que un puesto lleva en crítico más del tiempo configurado sin relevo aceptado
  Entonces se emite alerta al Coordinador
  Y aun así nadie se mueve automáticamente
```

### HU-D6 · Que el Bolsón vacío no desmantele una línea
> **Como** Coordinador, **quiero** que la extracción inversa respete un mínimo por línea, **para** que cubrir una línea no deje otra inoperante. *(§9.6, A5, B5)*

```gherkin
Escenario: Solo si la L8 está completamente vacía
  Dado que la L8 tiene aunque sea una persona disponible y compatible
  Cuando una línea necesita relevo
  Entonces se usa esa persona
  Y no se activa la extracción inversa

Escenario: Recorrido inverso
  Dado que la L8 no tiene ningún candidato viable
  Cuando se activa la extracción inversa
  Entonces se recorre L10, L9, L3, L5, L7, L6, L2, L1

Escenario: Línea inmune en el piso
  Dado que una línea está en su mínimo de operarios
  Cuando la extracción inversa la alcanza
  Entonces se la salta

Escenario: Planta agotada
  Dado que todas las líneas candidatas están en su mínimo
  Entonces las rotaciones se detienen
  Y el Coordinador recibe "Capacidad crítica de planta agotada. Requiere intervención humana."
```

## 4.5 Épica E — Movimiento entre líneas

### HU-E1 · Mover a alguien con confirmación en los dos extremos
> **Como** Supervisor, **quiero** que un traslado no se dé por hecho hasta que el destino confirme, **para** que el sistema y la planta no se desincronicen. *(Parte X, §12.7)*

```gherkin
Escenario: Despacho
  Dado que quiero enviar a alguien a otra línea
  Entonces solo puedo despachar a quien esté físicamente en mi línea y disponible
  Y al despachar se registra la hora exacta de salida

Escenario: Tránsito inmune
  Dado que una persona está en tránsito
  Cuando otro supervisor intenta capturarla o reasignarla
  Entonces el sistema lo impide
  Y explica que está en tránsito con destino comprometido

Escenario: Recepción
  Cuando el supervisor destino confirma la llegada
  Entonces se registra la hora exacta de llegada
  Y solo entonces se le asigna el puesto

Escenario: Tránsito caducado
  Dado que un tránsito supera la duración máxima configurada
  Entonces se alerta al origen, al destino y al Coordinador
  Y el puesto sigue reservado, marcado "Relevista demorado"
  Y nadie se mueve automáticamente
  Y el Coordinador puede cancelar el tránsito
```

## 4.6 Épica F — Contingencias y cierre

### HU-F1 · Registrar un paro técnico
> **Como** Supervisor, **quiero** registrar un paro con su causa y liberar a mi gente rotativa, **para** que no queden ociosos y quede constancia para mantenimiento. *(§11.1)*

```gherkin
Escenario: Clasificación en dos niveles
  Cuando registro un paro
  Entonces elijo primero una categoría general
  Y después una causa concreta filtrada por esa categoría
  Y escribo obligatoriamente qué observé antes de confirmar

Escenario: Efecto sobre el personal
  Cuando confirmo el paro
  Entonces los puestos fijos permanecen ocupados
  Y los puestos rotativos se liberan
  Y sus operarios se reubican en la L8
  Y cada puesto liberado muestra "Liberado por paro — operario en ensamble manual L8"

Escenario: Cronómetro persistente
  Cuando confirmo el paro
  Entonces arranca un cronómetro visible en todo momento
  Y sigue visible aunque navegue a otras partes de la aplicación
  Y solo se detiene cuando reanudo la producción explícitamente

Escenario: El paro alimenta la estadística de inmediato
  Cuando confirmo el paro
  Entonces el tiempo de paro acumulado se actualiza en mi panel
  Y en el panel del Coordinador
```

### HU-F2 · Registrar desperdicio y producción al cierre de lote
> **Como** Supervisor, **quiero** registrar el material perdido separando su causa, **para** que quede claro si el problema es del proveedor o del mantenimiento. *(§11.3, C4, C5)*

```gherkin
Escenario: Separación por causa
  Cuando cierro el lote
  Entonces registro daño de origen y daño de proceso por separado
  Y registro la producción real del lote

Escenario: Umbral que exige justificación
  Dado que el daño de proceso supera el umbral configurado del volumen total
  Cuando intento confirmar el registro
  Entonces el sistema exige justificación escrita antes de permitirlo

Escenario: El registro alimenta la estadística de inmediato
  Cuando confirmo el cierre de lote
  Entonces la eficiencia y el desperdicio se recalculan en el servidor
  Y se reflejan en mi panel y en el del Coordinador
```

### HU-F3 · Ver la eficiencia en vivo
> **Como** Supervisor y como Coordinador, **quiero** ver la eficiencia en tiempo real, **para** corregir dentro del turno y no al final del día. *(§11.4, C4)*

```gherkin
Escenario: Fórmula
  Entonces la eficiencia se calcula como producción real dividida entre
  el tiempo efectivo de marcha multiplicado por el ritmo teórico del SKU
  Y el tiempo efectivo de marcha es el tiempo de turno transcurrido menos la suma de paros
  Y el ritmo teórico proviene del catálogo de SKU, nunca de un valor fijo

Escenario: Escala de tres tramos
  Entonces se presenta en óptimo, aceptable o crítico
  Y los umbrales son configurables
  Y es visible para el supervisor de la línea y para el Coordinador

Escenario: El cálculo vive en el servidor
  Entonces el mismo turno muestra la misma cifra en los dos paneles

Escenario: Honestidad del dato
  Dado que no hay registro de producción reciente
  Entonces la pantalla dice "estimada desde el último registro — hace N min"
  Y nunca muestra un número inventado
```

### HU-F4 · Cerrar el turno sin dejar a nadie en el aire
> **Como** Supervisor, **quiero** que el sistema me impida cerrar si queda algo sin resolver, **para** no dejar personas caminando hacia una línea que ya cerró. *(§2.2.12, C13)*

```gherkin
Escenario: Cierre bloqueado
  Dado que tengo un lote abierto, o gente en tránsito hacia mi línea,
  o gente mía en tránsito hacia fuera sin recibir
  Cuando intento cerrar el turno
  Entonces el sistema lo impide
  Y lista exactamente qué lo bloquea y a quién debo llamar

Escenario: Cierre correcto
  Cuando cierro sin bloqueos
  Entonces mi personal asignado pasa a "fuera de turno"
  Y se persiste el último puesto ocupado de cada persona
  Y se cancelan los relevos pendientes de mi línea
  Y se liberan los puestos fijos
  Y caducan los descartados de mis puestos

Escenario: Cierre forzado por el Coordinador
  Dado que soy Coordinador
  Cuando fuerzo el cierre de una línea bloqueada
  Entonces el sistema exige el formulario de justificación
  Y sin él no se ejecuta
```

## 4.7 Épica G — Padrón y excepciones

### HU-G1 · Registrar restricciones médicas
> **Como** Coordinador, **quiero** registrar las capacidades físicas prohibidas que dicta Enfermería con su vigencia, **para** que el sistema las aplique solo y solo mientras estén vigentes. *(§2.1.6, §7.2, C14)*

```gherkin
Escenario: Registro con origen y vigencia
  Cuando registro una restricción
  Entonces indico la capacidad prohibida, la fuente, la fecha de dictamen
  Y la fecha de inicio y, si es temporal, la fecha de fin

Escenario: Nunca se borra
  Cuando una restricción deja de aplicar
  Entonces se cierra con fecha de fin
  Y sigue constando en el historial
```

### HU-G2 · Ejecutar una excepción con justificación
> **Como** Coordinador, **quiero** poder atender un acuerdo hablado con un trabajador, **para** resolver situaciones reales sin que el sistema se vuelva un obstáculo — dejando constancia. *(§2.1.9, A6)*

```gherkin
Escenario: Formulario obligatorio
  Cuando ejecuto cualquier excepción
  Entonces el sistema exige motivo de catálogo y texto libre
  Y sin el formulario completo la operación no se ejecuta
  Y queda auditado con mi identidad, la hora y la operación afectada

Escenario: Lo que la excepción nunca salta
  Cuando ejecuto una excepción
  Entonces sigue sin poder violar una restricción médica
  Y sigue sin poder violar la compatibilidad de categoría
```

### HU-G3 · Cubrir un puesto fijo que queda vacante en operación
> **Como** Supervisor, **quiero** cubrir de inmediato la máquina que quedó sin operador, **para** que la línea no se detenga. *(C15)*

```gherkin
Escenario: Escalera de cobertura
  Dado que un titular Operador A se retira con el turno arrancado
  Entonces el puesto queda "vacante crítica en operación"
  Y el sistema busca un Operador B en este orden
    | N1 | disponible en el Bolsón                     |
    | N2 | en puesto rotativo de la misma línea        |
    | N3 | en puesto rotativo de otra línea            |
    | N4 | no hay ninguno: alerta al Coordinador       |

Escenario: N1 encabeza la cola de la L8
  Dado que hay un Operador B disponible en el Bolsón
  Entonces la solicitud aparece al frente de la cola de la L8
  Por delante de cualquier solicitud por fatiga

Escenario: N2 lo ejecuta el supervisor de la línea
  Cuando tomo un Operador B de un rotativo de mi línea
  Entonces ese rotativo queda "rotativo descubierto"
  Y entra a la cola de relevos pendientes a prioridad normal

Escenario: N3 solo lo ejecuta el Coordinador
  Dado que el único Operador B disponible está en otra línea
  Entonces yo no puedo ejecutarlo
  Y la operación corresponde al Coordinador, con justificación
  Y recorre la jerarquía de proximidad desde mi línea
  Y el supervisor de origen recibe notificación

Escenario: Guarda anti-dominó
  Cuando un rotativo queda descubierto por esta cobertura
  Entonces entra a la cola a prioridad normal
  Y no dispara una segunda extracción de emergencia

Escenario: Piso de seguridad
  Dado que todas las líneas candidatas están en su mínimo
  Entonces no se extrae de ninguna
  Y se alerta al Coordinador, que puede forzar con justificación
```

### HU-G4 · Devolver el puesto a un titular que reaparece
> **Como** Supervisor, **quiero** decidir si devuelvo la máquina al titular que llega tarde, **para** aplicar el criterio que el sistema no tiene. *(C1, §12.5)*

```gherkin
Escenario: Oferta, no automatismo
  Cuando registro al titular que llega
  Y su puesto fijo está cubierto por un suplente
  Entonces el sistema ofrece "Devolver puesto al titular"
  Y puedo declinar y dejar al suplente

Escenario: Destino del suplente liberado
  Cuando acepto devolver el puesto
  Entonces el puesto muestra "Titular reincorporado — suplente liberado"
  Y el Operador B liberado recibe como destino un puesto rotativo fatigado de mi línea
  O, si no hay, el de la línea más cercana según proximidad
  O, si no hay ninguno, la L8
```

## 4.8 Épica H — Comportamiento sin conexión

### HU-H1 · Que la app se vuelva defensiva al perder red
> **Como** Supervisor, **quiero** que el sistema me impida dar órdenes que no puede confirmar, **para** no mandar a alguien a caminar y que después se rechace. *(§12.1)*

```gherkin
Escenario: Bloqueo defensivo
  Dado que pierdo la conexión
  Entonces se bloquea todo movimiento de personal entre líneas
  Y se bloquea el registro de nuevas asignaciones
  Y se muestra un aviso permanente e inequívoco
  Y los puestos afectados muestran
  "Pendiente de sincronización — no mover al personal hasta recuperar la red."

Escenario: No se encola nada
  Dado que estoy sin conexión
  Cuando intento una operación bloqueada
  Entonces no queda pendiente de envío
  Y el sistema me dice que no se puede hacer ahora

Escenario: La consulta sigue funcionando
  Dado que estoy sin conexión
  Cuando abro la malla de mi línea
  Entonces la veo con los últimos datos conocidos
  Y con el sello "Datos de hace N min"

Escenario: Datos demasiado viejos
  Dado que los datos superan la antigüedad máxima configurada
  Entonces aparece un banner permanente
  Y los datos se muestran visiblemente degradados

Escenario: Sin dependencias externas
  Cuando la app opera sin red
  Entonces se ve y se comporta igual que conectada
  Salvo por las operaciones que requieren el servidor
```

## 4.9 Épica I — Notificaciones

### HU-I1 · Enterarme aunque no tenga la app abierta
> **Como** Supervisor y como Coordinador, **quiero** recibir los avisos aunque no tenga la app abierta, **para** no perderme un relevo o un tránsito entrante mientras estoy en otra cosa. *(D5)*

```gherkin
Escenario: Entrega con la app en segundo plano
  Dado que tengo la app en segundo plano
  Cuando llega una notificación
  Entonces la recibo en el teléfono

Escenario: Ningún dato sale de la planta
  Cuando se entrega cualquier notificación
  Entonces viaja únicamente por el servidor de planta
  Y ningún dato de personal sale hacia un servicio de terceros

Escenario: El sistema no miente sobre lo que no entregó
  Dado que una notificación crítica no se acusa en el tiempo configurado
  Entonces escala al Coordinador
  Y aparece en su panel como "supervisor no localizable"
```

---

# 5 · Métricas de éxito (KPIs)

> ⚠ **`PENDIENTE-E7`.** §1.1 establece que hoy "todo se estima al final del día", así que se asume que **no existe línea base**. Los objetivos de abajo son **propuesta del equipo técnico**, no metas acordadas con el negocio. Se proponen medir la línea base durante las **dos primeras semanas de operación** y fijar entonces las metas definitivas.

## 5.1 KPIs primarios — miden si el producto resolvió el problema

| # | KPI | Definición | Origen del problema | Objetivo propuesto |
|---|---|---|---|---|
| **K1** | **Personas sin ubicación conocida** | Personas en estado indeterminado al cierre de turno | *"Nadie sabe dónde está la gente"* §1.1 | **Cero.** Es binario: cualquier valor distinto de cero es un defecto |
| **K2** | **Asignaciones que violan una restricción médica** | Asignaciones consolidadas incompatibles con una restricción vigente | *"Se asigna gente a puestos que no debería ocupar"* §1.1 | **Cero.** Regla dura: cualquier incidencia es un fallo crítico |
| **K3** | **Puestos que superan el umbral crítico de fatiga** | Puestos-turno que alcanzan crítico, sobre el total de puestos rotativos-turno | *"El desgaste no se controla"* §1.1 | Reducción sostenida contra la línea base de las 2 primeras semanas |
| **K4** | **Tiempo ocioso durante paros** | Minutos-persona de operarios rotativos sin reubicar mientras su línea está en paro | *"Se pierde tiempo productivo"* §1.1 | Reducción sostenida contra línea base |
| **K5** | **Turnos con registro completo** | Turnos cerrados con eficiencia, paros, desperdicio y producción registrados | *"No hay memoria"* §1.1 | **100 %** de turnos cerrados |

## 5.2 KPIs de operación — miden si el sistema se usa de verdad

| # | KPI | Por qué importa |
|---|---|---|
| **K6** | **Tiempo de estabilización de la línea** — minutos desde el gatillo de arranque hasta que la línea tiene su cobertura completa | Es el momento más caótico de la jornada (§8.4). Si no mejora, la ventana de arranque no está bien calibrada |
| **K7** | **Tiempo de resolución de relevo** — desde que un puesto entra en la cola hasta que el relevista queda asignado | Mide si el motor de relevos funciona en la práctica y no solo en el papel |
| **K8** | **Duración real de traslado por par de líneas** — mediana de salida a llegada | §12.7 lo pide explícitamente. Es lo que permite calibrar `duracion_maxima_transito` (B11) y validar la jerarquía de proximidad (A1) |
| **K9** | **Tasa de rechazo de propuestas en la L8** | Una tasa alta significa que el ranking de B2 no coincide con el criterio real del supervisor, y hay que revisarlo |
| **K10** | **Tasa de rechazo de recepción** | Una tasa alta indica descoordinación entre líneas o uso del rechazo como atajo (C10) |
| **K11** | **Excepciones del Coordinador por turno** | Muchas excepciones significan que una regla del sistema no encaja con la operación real. Es una señal de diseño, no de mal uso |

## 5.3 KPIs de salud del sistema

| # | KPI | Objetivo propuesto |
|---|---|---|
| **K12** | **Notificaciones no entregadas** | Bajo el 1 %. Toda no entrega debe estar visible como "supervisor no localizable" (D5) |
| **K13** | **Tiempo en modo defensivo por dispositivo-turno** | A vigilar: si es alto, el problema es la red de planta, no la app |
| **K14** | **Rechazos por concurrencia** | A vigilar: si son frecuentes, hay que revisar cómo se reparte el personal en la sala de espera |
| **K15** | **Operaciones sin traza de auditoría** | **Cero.** §12.7 no admite excepciones |

## 5.4 Criterio de fracaso

Un KPI que ningún tablero suele declarar, y que en este producto es el más importante:

> **Si el supervisor sigue llevando un papel en el bolsillo, el producto falló** — por muy verdes que estén los demás indicadores.

Se mide preguntándolo directamente en las revisiones de las primeras cuatro semanas.

---

# 6 · Trazabilidad — requisitos frente a fuente

| Sección PRD | Origen |
|---|---|
| §1 Propósito | §1.1, §1.2, §1.3 |
| §2 Usuarios | Parte II, §2.3, C7 |
| §3.2 Alcance MVP | §2.1, §2.2, Partes VII–XI |
| §3.3 Fuera de alcance | C3, C4, C7, C14, §12.1, Anexo §1 |
| §4.1 Épica A | §8.1, §8.3, §2.3, B8 |
| §4.2 Épica B | §8.4, §8.5, §12.2 |
| §4.3 Épica C | Parte VII, A4, B1, B6, B12 |
| §4.4 Épica D | Parte IX, A1, A5, A9, B2–B5, B9, B10, D1, D2 |
| §4.5 Épica E | Parte X, B11, §12.7 |
| §4.6 Épica F | Parte XI, C4, C5, C13 |
| §4.7 Épica G | §2.1.6, §2.1.9, A6, C1, C14, C15 |
| §4.8 Épica H | §12.1, D4 |
| §4.9 Épica I | D5 |
| §5 KPIs | §1.1 (los cinco problemas), §12.7, `⚠ PENDIENTE-E7` |
