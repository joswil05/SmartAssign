# SmartAssign — Especificación Funcional

**Concepto, lógica de negocio y motores de asignación.**
Versión 3.3 · 2026-08-08

> Este documento describe **qué debe hacer el sistema y por qué**. No prescribe arquitectura, tecnologías, estructura de datos, organización del código ni diseño de interfaz.

---

# PARTE I · QUÉ ES SMARTASSIGN

## 1.1 El problema

Una planta industrial reparte cada día ~160 trabajadores entre 10 líneas de producción. Hoy ese reparto se hace de palabra y en papel, y produce cinco problemas:

**Nadie sabe dónde está la gente.** Entre que un trabajador sale de una línea y llega a otra, desaparece del control. Dos supervisores pueden estar contando con la misma persona a la vez.

**Se asigna gente a puestos que no debería ocupar.** Enfermería dicta restricciones médicas, pero esa información vive en una carpeta, no en el momento en que el supervisor decide.

**El desgaste no se controla.** Alguien puede pasar cuatro horas seguidas en una tarea repetitiva, o repetirla dos días seguidos, sin que nada lo señale.

**Se pierde tiempo productivo.** Cuando una línea se detiene por avería, su personal queda ocioso porque nadie tiene la visión de conjunto para reubicarlo.

**No hay memoria.** Cuánto se paró, por qué, cuánto material se desperdició, qué tan eficiente fue el turno: todo se estima al final del día.

## 1.2 Qué resuelve

SmartAssign convierte el reparto de personal en un proceso **controlado, trazable y verificable**:

- En todo momento se sabe **dónde está cada persona** y en qué situación.
- Las **reglas de seguridad ocupacional se aplican solas**.
- El **desgaste es visible** antes de ser un problema.
- El **tiempo muerto se reduce** reubicando personal en cuanto una línea se detiene.
- Cada turno deja un **registro medible**.

## 1.3 El principio que gobierna todo

> **El sistema nunca miente sobre lo que está pasando.**

Si algo no se puede hacer, dice **por qué** y **qué hacer para desbloquearlo**, en lenguaje de planta. Un sistema que rechaza sin explicar entrena al supervisor a ignorarlo y resolver por fuera — y ahí deja de servir.

---

# PARTE II · LOS DOS TIPOS DE USUARIO

El sistema tiene exactamente **dos roles**, con alcances opuestos.

## 2.1 El Coordinador — visión global

Una persona por planta. **Ve y opera sobre las 10 líneas.** Planifica, decide la estructura del día y mantiene el padrón.

**Funciones que debe tener en el MVP:**

1. **Planificar la jornada:** definir qué líneas operan mañana, qué SKU corre cada una y qué turnos se cubren.
2. **Asignar un supervisor a cada línea activa.**
3. **Definir y reordenar la prioridad de las líneas** en caliente.
4. **Gatillar el arranque del turno**, que dispara el motor de asignación inicial.
5. **Ver el estado de las 10 líneas en tiempo real:** cobertura, déficit, paros, eficiencia.
6. **Gestionar el padrón de personal:** alta, edición, baja y reactivación; datos generales, habilidades técnicas, aptitudes, estado de disponibilidad, vacaciones, subsidios y demás ausencias justificadas; registrar restricciones médicas; marcar doble turno.
7. **Gestionar supervisores:** alta, baja y reasignación de línea.
8. **Intervenir sobre cualquier línea:** asignar, liberar y reubicar personal sin la restricción de línea propia.
9. **Mover personal entre líneas al margen de las restricciones de fatiga o de tiempo mínimo en el puesto**, para atender permisos o acuerdos hablados directamente entre el trabajador y el Coordinador. Es una excepción puntual al flujo normal de relevos — **las restricciones médicas y de compatibilidad de categoría nunca se omiten**, ni siquiera aquí.
10. **Acceder y editar todas las tablas de datos maestros del sistema** desde la interfaz: personal, líneas (estado de actividad y orden de prioridad), supervisores, puestos, SKUs y demás catálogos. Ningún dato maestro puede quedar disponible solo por fuera de la aplicación.
11. **Consultar el histórico:** jornadas anteriores, paros, desperdicio y eficiencia.

## 2.2 El Supervisor — una sola línea

Uno por línea activa. **Ve y opera exclusivamente su línea.** Trabaja desde un teléfono, de pie, con guantes, moviéndose por la planta.

> **El aislamiento es total y deliberado.** No puede ver ni tocar puestos ni personal de otras líneas. No es solo control de permisos: protege datos médicos y delimita de qué responde cada quien.

**Funciones que debe tener en el MVP:**

1. **Ver la malla de puestos de su línea** con ocupación, situación y fatiga de cada uno.
2. **Registrar operarios en puestos rotativos**, escaneando el gafete o buscando por nombre o ficha.
3. **Confirmar la identidad** de cada persona antes de consolidar el registro.
4. **Liberar a una persona** de su puesto hacia la Línea 8.
5. **Registrar un retiro temporal** por enfermería o contingencia.
6. **Solicitar relevo** para un puesto sin liberarlo todavía.
7. **Aceptar o rechazar la propuesta de relevista** — función exclusiva del supervisor de la L8: acepta o rechaza al candidato que el sistema propone de su propio personal para cubrir un puesto fatigado de otra línea.
8. **Recibir al relevista despachado desde la L8**, confirmando su llegada física y asignándolo al puesto fatigado; y **despachar al trabajador relevado** hacia el destino que el sistema sugiera — otro puesto fatigado de su propia línea; si no hay, uno en la línea más cercana según la jerarquía de proximidad de relevos; si no hay ninguno, hacia la L8.
9. **Consultar las restricciones médicas** del personal de su línea.
10. **Registrar paros técnicos** y reanudar producción.
11. **Registrar el desperdicio de material** al cierre del lote.
12. **Cerrar el turno** de su línea.

> **Nota:** ambas listas son **capacidades**, no pantallas. Cómo se organicen visualmente es una decisión de diseño posterior.

## 2.3 La regla de supervisor único

**Cada línea activa tiene un supervisor, y cada supervisor tiene una línea.**

La línea del supervisor **la determina el sistema** según la asignación del Coordinador. **Nunca la elige él de una lista.** Si no tiene línea asignada, no puede operar, y el sistema debe decírselo con claridad y remitirlo al Coordinador.

---

# PARTE III · ESTRUCTURA DE LA PLANTA

## 3.1 Las líneas

La planta tiene **10 líneas de producción**, identificadas como **L1 a L10**.

Cada línea, en una jornada dada, puede estar:

| Situación | Significado |
|---|---|
| **Activa** | Tiene SKU planificado y opera hoy |
| **Inactiva** | Sin SKU planificado. Sus puestos quedan fuera de operación |
| **En producción** | Activa y corriendo normalmente |
| **En paro** | Detenida por contingencia técnica |
| **En arranque** | En proceso de llenado inicial |
| **En limpieza** | Cambio de producto entre lotes |

## 3.2 La Línea 8 — el Bolsón

**La Línea 8 no es una línea más.** Cumple una función estructural única: es el **Bolsón**, el pulmón de recursos humanos de la planta.

En la L8 hay mesas de **ensamble manual**: tareas siempre disponibles, sin urgencia, que absorben trabajo. El personal que espera ahí **no está ocioso**: está produciendo mientras queda disponible para ser requerido en cualquier momento.

Su papel es doble:

- **Absorbe** al personal que sale de cualquier línea (por relevo, por paro, por línea suspendida).
- **Provee** personal a cualquier línea que lo necesite.

### La regla Hub-and-Spoke

> **Todo movimiento de personal entre líneas pasa por la Línea 8.** Nunca se transfiere personal directamente de una línea activa a otra.

**Por qué:** sin esta regla, cubrir un hueco en L4 abre otro en L2, que se cubre desde L6, que abre otro... El resultado es un efecto dominó con gente cruzándose en los pasillos, notificaciones simultáneas por toda la planta y ningún supervisor entendiendo por qué se le fue su personal. Con la L8 de por medio, **cada movimiento tiene un solo origen y un solo destino**, y siempre es explicable.

**La regla no tiene excepción para intercambiar dos puestos ya ocupados**, ni siquiera dentro de una misma línea: intercambiar directamente a dos operarios exige, en la práctica, vaciar un puesto para llenar el otro, deteniendo la producción en ese instante. Eso solo lo resuelve la L8 (Parte IX).

**Sí existe una excepción acotada.** En la planta no hay puestos rotativos vacíos durante la operación normal — todos están ocupados desde el arranque. Lo que sí hay son **puestos fatigados** que ya necesitan relevo por su cuenta. Cuando un trabajador queda libre por haber sido relevado, el sistema puede convertirlo directamente en el relevista de **otro puesto fatigado** cercano — de su propia línea primero, y si no, de la línea más cercana según la jerarquía de proximidad (§9.4–§9.5) — sin pasar por la L8. No es la transferencia entre líneas activas que esta regla prohíbe: ese puesto ya estaba pidiendo relevo de todas formas, así que no se abre ningún hueco nuevo, solo se resuelve uno que ya existía con la persona que tenía más cerca. Si no hay ningún puesto fatigado disponible en el recorrido, el destino por defecto es la L8.

## 3.3 La jerarquía de prioridad de líneas

**Las líneas no valen lo mismo.** Existe un orden de prioridad que determina cuál se llena primero cuando el personal no alcanza para todas.

**Orden base:**

```
L4  >  L1  >  L2  >  L6  >  L7  >  L5  >  L3  >  L8  >  L9  >  L10
```

**Esta jerarquía gobierna el motor de asignación inicial y la extracción inversa.** El motor de relevos, en cambio, **no** usa esta jerarquía para decidir el nuevo destino del trabajador relevado — usa la **cercanía física entre líneas** (§9.5). Son dos criterios distintos que conviene no confundir: uno mide qué línea importa más, el otro mide qué línea está más cerca.

> **La prioridad debe ser configurable en caliente por el Coordinador, nunca fija en el código.** Responde a realidades del negocio que cambian de un día para otro: un pedido urgente de un cliente clave, una avería mecánica, una alerta de calidad que obliga a reforzar una línea concreta.

**Cómo lee la jerarquía cada motor:**

| Motor | Recorre la jerarquía... | Porque... |
|---|---|---|
| **Asignación inicial** | **De mayor a menor** — L4 primero | Las líneas más importantes reclaman primero el personal escaso |
| **Extracción de emergencia** | **De menor a mayor** — L10 primero | Si hay que quitarle gente a alguien, se le quita a quien menos impacto tiene |
| **Reasignación del relevado** | **No usa esta jerarquía** — usa la jerarquía de proximidad física (§9.5) | El criterio relevante ahí es cuánto debe caminar la persona, no cuánto importa la línea |

La L8 ocupa una posición intermedia en la jerarquía, pero **como destino de personal, no como origen de producción**: su prioridad indica cuánto se resiste a quedarse sin gente.

---

# PARTE IV · EL PERSONAL

## 4.1 Operadores y operarios — la distinción fundamental

> **"Operador" y "operario" NO son sinónimos.** Son las dos familias de personal, y la diferencia gobierna toda la lógica de asignación.

### Los Operadores — personal técnico

Cubren **puestos fijos**. Tienen habilitación técnica y están ligados a máquinas concretas.

| Categoría | Naturaleza |
|---|---|
| **Operador A** | Máxima habilitación técnica. Indispensable. Se ancla automáticamente a su máquina crítica al arrancar el turno. |
| **Operador B** | Calificado de **alta versatilidad**. Puede cubrir cualquier puesto fijo como suplente, y también puestos rotativos. |
| **Operador C** | Técnico júnior **en entrenamiento**, bajo tutoría. |
| **Averiero** | Personal técnico de soporte y resguardo mecánico. |

### Los Operarios — personal general

Cubren **puestos rotativos**. No requieren habilitación técnica específica, pero su trabajo es físicamente exigente y repetitivo. **Son quienes rotan por fatiga.**

Es la categoría más numerosa y la que da sentido al sistema de relevos.

### El Operador B — la pieza clave

> **El Operador B es el comodín del sistema.**

Es el único que se mueve entre las dos familias: cubre un puesto fijo cuando falta su titular, y también puede reforzar puestos rotativos. **Sin esta categoría, la ausencia de un Operador A deja la línea coja sin remedio.**

Por eso el Operador B es el **recurso más disputado** de la planta, y por eso el motor de asignación inicial debe repartirlos siguiendo estrictamente la jerarquía de prioridad.

### Personal de liderazgo

Supervisores, jefes de turno, coordinadores y analistas. **Nunca son propuestos automáticamente** por ningún motor. Pero **sí pueden ser asignados manualmente** en un déficit crítico: el supervisor debe seleccionar primero el puesto de destino y después registrar a la persona. Es un acto deliberado de dos pasos.

## 4.2 Matriz de compatibilidad

| Tipo de puesto | Quién puede ocuparlo |
|---|---|
| Puesto fijo de **Operador A** | Operador A · **Operador B** |
| Puesto fijo de **Averiero** | Averiero · **Operador B** |
| Puesto fijo de **Operador C** | Operador C · **Operador B** · Operador A |
| **Puesto rotativo** | Operario · **Operador B** |

**Los Operadores A y los Averieros no bajan a puestos rotativos:** su habilitación se necesita en su máquina, y ponerlos a tareas generales desperdicia un recurso escaso.

---

# PARTE V · LOS PUESTOS

Solo existen **dos tipos**, y su diferencia gobierna casi toda la lógica.

## 5.1 Puestos fijos

Puestos **técnicos**, ligados a una máquina o función específica.

- Los cubren **operadores** (A, B, C, Averiero).
- Cada uno tiene un **titular**: quien normalmente lo ocupa.
- **Se asignan automáticamente** al arrancar el turno. El supervisor no interviene.
- **No rotan.** Quien lo ocupa permanece todo el turno.
- **No acumulan fatiga** a efectos de relevo.
- **Durante un paro permanecen ocupados**: son quienes ejecutan la reparación.

## 5.2 Puestos rotativos

Puestos de **tarea general**, físicamente exigentes y repetitivos.

- Los cubren **operarios**, y Operadores B si hace falta.
- **Inician el turno vacíos.** El supervisor los llena a pie de línea.
- **Rotan durante el turno** por desgaste.
- **Acumulan fatiga**: son la razón de ser del sistema de relevos.
- **Durante un paro se vacían**: su personal va a la L8 para no quedar ocioso.

## 5.3 Situación de un puesto

| Situación | Significado |
|---|---|
| **Libre** | Activo y sin ocupante. Se puede asignar. |
| **Ocupado** | Tiene a alguien trabajando. |
| **Vacante crítica** | Libre, pero el motor no encontró titular ni suplente. Prioridad máxima. |
| **Fuera de operación** | El puesto **no existe para el SKU de hoy**, o su línea no está activa. |

> **"Fuera de operación" no es "libre" ni "ocupado".** Es una tercera categoría. Si el sistema la confunde con "ocupado", dirá que la línea está llena cuando está vacía. Si la confunde con "libre", intentará llenar puestos que hoy no existen y contará déficit donde la cobertura está completa.

---

# PARTE VI · CICLO DE VIDA DEL TRABAJADOR

En todo momento, cada persona está en **una** de estas situaciones:

| Situación | Significado |
|---|---|
| **Fuera de turno** | No se ha presentado, o su jornada no ha comenzado |
| **Presente, sin asignar** | Marcó entrada. En sala de espera, disponible |
| **Asignado** | Ocupa un puesto y está produciendo |
| **En tránsito** | Caminando entre dos puntos de la planta |
| **En Bolsón** | Trabajando en ensamble manual en L8, disponible |
| **Retirado temporalmente** | Fuera del flujo por enfermería o contingencia |
| **Ausente justificado** | Vacaciones, permiso, cita médica, subsidio, accidente laboral |

## 6.1 Reglas del ciclo

**El tránsito es inmune.** Mientras alguien camina entre dos puntos, **ninguna otra terminal puede capturarlo ni reasignarlo**. Su destino ya está comprometido. Sin esta protección, un supervisor de paso "roba" a alguien que otro ya está esperando, y ninguno se entera hasta que falta gente.

**Quien está fuera de turno SÍ puede ser asignado.** Es la situación normal de quien cerró ayer y hoy se presenta. Bloquearlo rompe el arranque.

**Quien está ausente justificado NUNCA puede ser asignado.** Sin excepciones.

**Quien está asignado, en tránsito o retirado no puede recibir otra asignación.**

---

# PARTE VII · REGLAS DE VALIDACIÓN

Se evalúan **siempre**, sin importar si asigna un motor automático o el supervisor a mano.

> **Se validan de forma central y autoritativa.** La interfaz puede anticipar el resultado para dar buena experiencia, pero **la decisión final nunca puede quedar del lado del dispositivo**. Un dispositivo manipulado, un reintento o un fallo de red no pueden ser vía para saltarse una regla de seguridad.

## 7.1 Orden de evaluación

El **primer rechazo detiene el proceso**, para poder decir exactamente qué falló:

1. ¿El puesto sigue libre?
2. ¿La persona sigue disponible?
3. ¿Su categoría es compatible con el tipo de puesto?
4. ¿Sus restricciones médicas lo permiten?
5. ¿El perfil requerido por el puesto lo permite?
6. ¿No repitió esta misma tarea ayer?
7. ¿La ventana de arranque lo permite?

## 7.2 Restricciones médicas — regla dura

**Enfermería define, el sistema obedece.**

Cada persona tiene registradas las **capacidades físicas que tiene prohibidas**. Cada puesto declara las **capacidades que exige**. Si hay coincidencia, la asignación se **deniega**.

- La verificación es **general**, sobre todas las restricciones registradas. No puede limitarse a un tipo concreto de esfuerzo.
- **Esta regla nunca se relaja**, en ningún nivel de ningún motor, por ninguna urgencia operativa.

## 7.3 Perfil requerido — regla blanda

Algunos puestos declaran un perfil preferente por razones técnicas o ergonómicas.

- Solo aplica **si el puesto lo declara**.
- Si el dato de la persona no está registrado, **la regla no se aplica**. Nunca se infiere ni se deduce: es preferible no aplicar una restricción que aplicarla sobre una suposición.
- **Puede ceder** ante la necesidad, según la escalera del §8.5. Es una preferencia técnica, no una condición de seguridad.

## 7.4 Regla de no repetición de 24 horas

**Nadie repite dos días seguidos la misma tarea desgastante.** Si la actividad del puesto coincide con la que esa persona hizo al cerrar su jornada anterior, se deniega.

## 7.5 Concurrencia

**Si dos supervisores intentan capturar a la misma persona a la vez, gana uno y el otro recibe un rechazo claro.** Nunca puede ocurrir que ambos crean que la tienen.

Toda operación debe aplicarse **completa o no aplicarse**: no puede quedar el puesto ocupado y la persona libre, ni al revés.

---

# PARTE VIII · MOTOR DE ASIGNACIÓN INICIAL

Es el proceso más importante del sistema.

## 8.1 Etapa 1 — Planificación *(día anterior, Coordinador)*

**Se define qué líneas operan y con qué SKU.** Una línea sin SKU planificado queda **inactiva**: sus puestos pasan a *fuera de operación*, y el personal habitual de esa línea que se presente al día siguiente **es absorbido por la L8**.

**Se asigna un supervisor a cada línea activa.**

**Se revisa la cobertura prevista** por línea, considerando ausencias ya conocidas. El Coordinador equilibra la planta moviendo personal antes de que empiece el turno.

**Se confirma la planificación.**

## 8.2 Etapa 2 — Entrada de personal *(minuto cero)*

Los trabajadores marcan entrada física y pasan a *presente, sin asignar*.

**A cada persona se le registra en qué línea se encuentra físicamente**, deducido de dónde trabajó habitualmente. Este dato es lo que permite después distinguir a quien está enfrente del supervisor de quien está al otro extremo de la planta. **Es la base de la ventana de arranque (§8.4).**

## 8.3 Etapa 3 — Barrido automático de puestos fijos

El Coordinador **gatilla el arranque del turno**. El motor recorre entonces todos los puestos fijos de las líneas activas.

### El recorrido sigue la jerarquía de prioridad

> **El motor procesa las líneas de mayor a menor prioridad: L4 primero, después L1, L2, L6, L7, L5, L3, L8, L9 y L10.**

**Por qué el orden importa:** los Operadores B disponibles son un recurso **escaso**. Si el motor recorriera las líneas en orden arbitrario, una línea de baja prioridad podría consumir al último Operador B disponible y dejar sin cubrir un puesto crítico de L4. Recorriendo por prioridad, **las líneas más importantes reclaman primero**, y el déficit —si lo hay— cae donde menos daño hace.

### Para cada puesto fijo

**Si el titular está presente → se le asigna automáticamente.**
Micro-copia: *"Asignado automáticamente por asistencia"*.

**Si el titular está ausente → se busca un Operador B disponible.**
Al asignar al suplente, el puesto registra **dos identidades**: quién lo ocupa ahora y **quién es su titular original**.
Micro-copia: *"Cubierto por suplente — titular ausente"*.

> **Por qué se conserva el titular:** cuando reaparece —vuelve de enfermería, llega tarde— el sistema debe saber cuál era su máquina para devolvérsela. Si al asignar al suplente se pierde ese dato, la información desaparece y todo queda a que alguien lo recuerde.

**Si no hay titular ni Operador B → vacante crítica.**
Debe destacarse por encima de las vacantes normales: es un hueco técnico que la línea necesita cubrir sí o sí.

**El resto del personal queda disponible**, esperando a que su supervisor lo recoja.

## 8.4 Etapa 4 — Llenado de puestos rotativos *(Supervisor)*

Los puestos rotativos **empiezan vacíos**. Cada supervisor va físicamente a la sala de espera, recoge a su grupo, lo lleva a su línea y ahí registra a cada operario en su puesto.

### La ventana de arranque local aislado — REGLA CRÍTICA

> **Durante los primeros minutos del turno, cada supervisor solo puede registrar a personas que estén físicamente en su línea.**

Dentro de esa ventana:

- Solo se admite a quien esté físicamente presente en esa línea.
- Los movimientos entre líneas quedan **bloqueados**.
- Cualquier desvío automático por prioridad queda **desactivado**.

> **Por qué existe:** el arranque es el momento de mayor movimiento físico de la jornada — decenas de personas caminando a la vez. Si en ese momento el sistema empieza a desviar gente hacia líneas de mayor prioridad, el resultado es caos: personas cruzándose en direcciones contradictorias, supervisores esperando a alguien que fue redirigido, y nadie con visión de lo que pasa.
>
> La ventana obliga a que **cada supervisor ordene primero a la gente que tiene enfrente**. Una vez las líneas están estabilizadas, se abren los movimientos y la jerarquía de prioridad vuelve a regir.

La duración de la ventana debe ser **configurable**.

## 8.5 Escalera de sugerencia de puesto

Cuando el supervisor registra a alguien **sin haber seleccionado antes un puesto**, el motor propone destino en este orden:

| Orden | Criterio |
|---|---|
| **1** | Un puesto libre de esta línea **cuyo titular sea esa misma persona**, cumpliendo todas las reglas |
| **2** | Ese mismo puesto de titular, cumpliendo todo **salvo el perfil preferente** |
| **3** | Cualquier puesto libre compatible, cumpliendo todas las reglas |
| **4** | Cualquier puesto libre compatible, **salvo el perfil preferente** |
| — | Si nada aplica: **no proponer nada** y explicar qué regla lo impidió |

> **La lógica del orden:** devolver a cada quien su puesto habitual es lo que menos fricción produce — conoce la tarea y la hace mejor. El perfil preferente cede porque es preferencia técnica. **Las restricciones médicas no ceden en ningún nivel.**

Cuando no encuentra nada, **debe decir cuál regla lo impidió**. Sin esa información el supervisor no puede resolverlo.

---

# PARTE IX · MOTOR DE RELEVOS

El segundo motor del sistema. **Radica en la Línea 8.**

## 9.1 La fatiga

Los puestos rotativos desgastan. El sistema mide **cuánto lleva cada operario en su puesto actual** y lo señala en tres niveles:

| Nivel | Significado |
|---|---|
| **Normal** | Sin alerta |
| **Relevo sugerido** | Se recomienda rotar. Aviso visible, no bloqueante |
| **Relevo crítico** | Límite ergonómico superado. Máxima prominencia |

Los umbrales son **configurables**. El avance hacia el límite debe verse de forma **continua**, no solo al cruzarlo: el supervisor necesita anticiparse.

**La fatiga solo aplica a puestos rotativos.** Los operadores en puestos fijos no entran en este cálculo.

## 9.2 El ciclo de relevo — el relevista siempre entra desde la L8

> **El relevista que cubre un puesto fatigado sale siempre de la L8.**

```
              LÍNEA CRÍTICA (ej. L4)
                        │
        ┌───────────────┴───────────────┐
        │                               │
        ▼                               │
   Relevista                        Relevado
     entra                            sale
        ▲                               │
        │                               ▼
        └──────────  LÍNEA 8  ──────────┘
                  (Bolsón, por defecto)
```

**El relevado, en cambio, no vuelve automáticamente a la L8.** En la planta no hay puestos rotativos vacíos — todos están ocupados desde el arranque. Lo que el sistema busca para el relevado es **otro puesto fatigado** al que pueda pasar a relevar de inmediato: primero dentro de su propia línea; si no hay ninguno fatigado ahí, recorre la jerarquía de proximidad (§9.5) buscando un puesto fatigado en la línea más cercana. **Solo si no encuentra ningún puesto fatigado en todo el recorrido, el relevado va a la L8.**

> **Por qué esto no reabre el efecto dominó:** el relevado no le quita el puesto a nadie que esté trabajando tranquilo — el puesto al que llega **ya estaba pidiendo relevo por su cuenta**, con o sin él. Solo se resuelve con la persona que tiene más cerca, en vez de esperar a que la L8 mande a alguien desde más lejos. Sigue prohibido extraer a alguien de un puesto que no lo necesita para tapar otro hueco: eso solo lo hace la extracción inversa (§9.6), bajo sus propias reglas.

## 9.3 La rotación es una decisión humana

> **El sistema señala, sugiere y advierte. El supervisor decide y ejecuta.**

No hay rotación automática. Cuando el sistema detecta fatiga, **avisa**; no mueve a nadie por su cuenta. El supervisor conoce el contexto que el sistema no ve: quién está rindiendo, quién acaba de volver de un descanso, qué tarea conviene no interrumpir.

Un sistema que mueve personal por su cuenta genera desconfianza y, en cuanto se equivoca una vez, deja de usarse.

## 9.4 Flujo del relevo

**Paso 1 — Detección y aviso.** Cuando un puesto rotativo alcanza el nivel de fatiga *"relevo sugerido"* (§9.1), el sistema notifica a **todos los supervisores de línea** como un aviso informativo — así saben lo que está pasando en la planta aunque no puedan actuar sobre ello. Un supervisor también puede marcar manualmente un puesto como *relevo solicitado* antes de llegar a ese umbral. En ambos casos, el puesto **no se libera todavía**: el operario sigue produciendo hasta que llegue su reemplazo.

> Liberar primero y buscar después deja el puesto descubierto durante la búsqueda. Marcar y esperar mantiene la producción hasta el momento del cambio.

**Paso 2 — Propuesta a la L8.** Solo el **supervisor de la L8** puede ejecutar un relevo. Para cada puesto con relevo pendiente, el sistema le propone el candidato compatible más apto entre su propio personal disponible, aplicando **todas** las reglas de la Parte VII.

**Paso 3 — Aceptación o rechazo por la L8.**

- **Si el supervisor de la L8 acepta:** el candidato queda **en tránsito** hacia la línea destino, y el puesto fatigado queda **reservado** para él.
- **Si rechaza:** el candidato **queda registrado como descartado para ese puesto concreto**, y el sistema carga otra sugerencia si hay alguna disponible.

> **Por qué se recuerda el rechazo:** el supervisor de la L8 puede tener motivos que el sistema no ve. Si el sistema insiste con el mismo candidato una y otra vez, deja de confiar en él. **Debe existir forma de limpiar la lista de descartados** de un puesto, para que un rechazo puntual no se vuelva un veto permanente e invisible.

**Paso 4 — Aviso a la línea destino.** Al aceptar la L8, el supervisor de la línea destino recibe la notificación de que **viene una persona en tránsito a relevar ese puesto fatigado concreto** — sabe, antes de que la persona llegue, a qué puesto va.

**Paso 5 — Llegada y asignación.** Cuando el relevista se presenta físicamente, el supervisor destino confirma la llegada y lo asigna al puesto fatigado. También puede rechazar la recepción, devolviendo a la persona a la L8 (Parte X).

**Paso 6 — Reasignación del relevado.** En la planta no hay puestos rotativos vacíos durante la operación — todos están ocupados desde el arranque. Por eso, en el mismo momento en que el supervisor destino confirma la llegada del relevista, el sistema le presenta al relevado el **puesto fatigado** más conveniente al que puede pasar a relevar de inmediato. El operario se acerca al supervisor a preguntar a dónde debe ir, y el supervisor ejecuta la sugerencia:

1. **Otro puesto fatigado compatible dentro de su misma línea** — el relevado pasa a relevar directamente a ese compañero, sin salir de la línea.
2. Si no hay ningún puesto fatigado en su línea, un puesto fatigado compatible en la línea **más cercana** según la jerarquía de proximidad (§9.5), y así sucesivamente recorriendo esa jerarquía.
3. Si no hay ningún puesto fatigado en todo el recorrido, el relevado va a la **L8** — a esperar disponible en el Bolsón.

Cuando el destino es una línea distinta, los puntos 2 y 3 se ejecutan bajo el proceso de despacho/tránsito/recepción de la Parte X, porque implican un desplazamiento físico real entre líneas. Cuando el destino es un puesto de la misma línea, lo asigna directamente el propio supervisor, sin ese trámite.

**Ejemplo — capacidad limitada de la L8 y relevo en cadena.** Supongamos que hay 5 puestos fatigados: 4 en L4 y 1 en L1. La L8 solo tiene personal disponible y compatible para cubrir 3 (por restricciones médicas, de categoría, o simplemente porque no tiene más gente en ese momento): envía 2 hacia L4 y 1 hacia L1. Desde que aceptó cada propuesta, esas 3 personas quedan en tránsito y sus puestos destino quedan reservados. Al llegar los 2 relevistas a L4, los 2 operarios relevados no van a la L8: como en L4 **siguen fatigados otros 2 puestos**, el sistema los sugiere como destino directo — cada relevado pasa a relevar a uno de esos 2 compañeros, resolviendo la fatiga de L4 sin gastar más personal de la L8. Si no quedara ningún puesto fatigado disponible para reubicarlos, irían a la L8. Y si la L8 se agota por completo antes de cubrir lo que falte, se activa la extracción inversa (§9.6).

## 9.5 Jerarquía de proximidad para relevos

> **La reasignación del relevado (Paso 6, §9.4) no sigue la jerarquía de prioridad de líneas (§3.3). Sigue la cercanía física entre líneas.**

Son dos criterios distintos y no deben confundirse: la jerarquía de prioridad dice **qué línea importa más**; la jerarquía de proximidad dice **qué línea está físicamente más cerca**. La primera gobierna quién reclama personal escaso primero; la segunda gobierna a dónde camina alguien que ya quedó libre.

**Orden físico de referencia:** L1, L2, L4, L6, L9, L7, L5, L8.

**Para cada línea, el recorrido de cercanía a evaluar es:**

| Línea | Orden de cercanía (de más a menos cercana) |
|---|---|
| L1 | L2, L4, L9, L10, L6, L3, L7, L5, L8 |
| L2 | L4, L1, L7, L9, L10, L3, L6, L5, L8 |
| L3 | L10, L9, L6, L7, L4, L2, L1, L5, L8 |
| L4 | L2, L1, L7, L9, L10, L6, L3, L5, L8 |
| L5 | L1, L2, L4, L7, L9, L10, L6, L3, L8 |
| L6 | L3, L10, L9, L7, L4, L2, L1, L5, L8 |
| L7 | L9, L10, L6, L3, L4, L2, L1, L5, L8 |
| L8 | No aplica — la L8 no recorre esta jerarquía. Es siempre el destino de respaldo, nunca busca "la línea más cercana". |
| L9 | L3, L10, L6, L7, L4, L2, L1, L5, L8 |
| L10 | L3, L9, L6, L7, L4, L2, L2, L5, L8 *(dato recibido con "L2" repetido — confirmar si el segundo valor debía ser L1)* |

> **Pendiente:** solo falta cerrar el dato de L10 — llegó con "L2" repetido y probablemente el segundo debía ser L1, que no aparece en ninguna otra posición de esa lista.

## 9.6 Extracción inversa — cuando el Bolsón se vacía

Si la L8 se queda sin personal disponible y una línea de alta prioridad necesita relevo, el sistema recurre a la **extracción inversa por jerarquía**:

> **Se busca personal en la línea activa de MENOR prioridad**, recorriendo la jerarquía al revés: L10, L9, L3, L5, L7, L6, L2, L1.

**Por qué al revés:** si hay que quitarle personal a alguien, se le quita a quien menos impacto produce en la operación global.

**Esto es distinto del caso de capacidad limitada del §9.4:** mientras la L8 tenga aunque sea una persona disponible y compatible, se usa esa persona antes de recurrir a la extracción inversa. La extracción inversa solo se activa cuando la L8 está completamente vacía de candidatos viables.

### El piso de seguridad

> **Ninguna línea puede quedar por debajo de un mínimo de operarios.** Al llegar a ese mínimo, la línea se declara **inmune a extracción**.

Esto impide que el mecanismo desmantele por completo una línea de baja prioridad para alimentar a una de alta.

**Si toda la planta está en el mínimo**, el sistema **detiene las rotaciones** y emite una alerta al Coordinador:

> *"Capacidad crítica de planta agotada. Requiere intervención humana."*

El mínimo es **configurable**.

## 9.7 Liberación manual

Al seleccionar un puesto ocupado, el supervisor puede:

- **Liberar a la L8** — el puesto queda libre; la persona pasa al Bolsón y su ubicación física se actualiza a L8. Es la rotación normal.
- **Registrar retiro temporal** — el puesto queda libre; la persona sale del flujo (enfermería, contingencia) y **no queda disponible** para nadie.

---

# PARTE X · MOVIMIENTO ENTRE LÍNEAS

Mover a alguien de una línea a otra es un proceso de **tres pasos con confirmación**, porque implica un desplazamiento físico real.

**Paso 1 — Despacho.** El supervisor de origen despacha a la persona hacia una línea destino. Solo puede despachar a quien esté **físicamente en su línea** y disponible (en Bolsón o sin asignar).

**Paso 2 — Tránsito.** La persona queda *en tránsito* con destino registrado. Es **inmune** durante el trayecto.

**Paso 3 — Recepción.** El supervisor destino **confirma que llegó físicamente**, y solo entonces se le asigna el puesto. También puede **rechazar la recepción**, devolviéndola a la L8.

> **Por qué la confirmación es obligatoria:** sin ella, el sistema daría por ocupado un puesto desde el despacho, aunque la persona tarde cinco minutos en llegar o se quede por el camino. La confirmación es lo que mantiene alineados el sistema y la realidad física.

---

# PARTE XI · CONTINGENCIAS Y CIERRE DE LOTE

## 11.1 Paros técnicos

**Clasificación en dos niveles.** Primero una categoría general (mecánico, eléctrico, calidad, falta de material) y después una causa concreta dentro de esa categoría, filtrada según la primera.

**Descripción obligatoria.** El supervisor debe escribir qué observó antes de confirmar. Es lo que convierte el registro en información útil para mantenimiento.

**Qué ocurre con el personal:**

- Los **puestos fijos permanecen ocupados**: los operadores técnicos son quienes ejecutan la reparación.
- Los **puestos rotativos se liberan** y sus operarios se reubican en la **L8**, para no quedar ociosos.

**Cronómetro visible y persistente.** Al confirmarse el paro arranca un cronómetro que permanece **visible en todo momento**, aunque el supervisor navegue por otras partes de la aplicación. Solo se detiene cuando **reanuda la producción** explícitamente.

**Cada paro queda registrado** con su duración, y alimenta el cálculo de eficiencia.

## 11.2 Cambio de SKU

Cuando una línea cambia de producto, cambia qué puestos necesita:

- Los puestos que el nuevo SKU **sí requiere** y estaban fuera de operación pasan a **libres**.
- Los que **ya no se requieren** pasan a **fuera de operación**; si tenían ocupante, esa persona va a la **L8**.

El sistema debe informar cuántos puestos se activaron y cuántos se desactivaron.

## 11.3 Registro de desperdicio

Al cierre del lote, el supervisor registra el material desperdiciado separado en **dos causas**:

- **Daño de origen** — material que llegó defectuoso: fallas de fábrica, daños de almacén o transporte.
- **Daño de proceso** — material destruido por la maquinaria durante la producción, normalmente por descalibración.

**La separación importa porque apuntan a responsables distintos:** la primera es problema de proveedor o logística; la segunda, de mantenimiento.

**Si el daño de proceso supera un umbral configurable del volumen total, el sistema exige justificación escrita** antes de permitir el registro.

## 11.4 Medición de eficiencia

```
Eficiencia = Producción real / (Tiempo efectivo de marcha × Ritmo teórico del SKU)

donde:  Tiempo efectivo de marcha = Tiempo de turno transcurrido − Suma de paros
```

Se presenta en una escala de tres tramos (óptimo / aceptable / crítico) con umbrales **configurables**, visible **tanto para el supervisor de la línea como para el Coordinador**, en tiempo real.

El ritmo teórico depende del SKU y proviene del catálogo, nunca de un valor fijo.

## 11.5 Doble turno

Una persona puede quedar marcada como **cubriendo dos turnos consecutivos**. Debe ser **visible para el supervisor**, porque el cálculo de fatiga, que cuenta desde el inicio del turno actual, no refleja el desgaste real de quien lleva el doble de tiempo trabajando.

---

# PARTE XII · REGLAS TRANSVERSALES

## 12.1 Comportamiento sin conexión

La red de planta es inestable y tiene zonas muertas. Al perder conexión, **la interfaz se vuelve defensiva, no optimista**:

- Se **bloquea** todo movimiento de personal entre líneas.
- Se **bloquea** el registro de nuevas asignaciones.
- Se muestra un **aviso permanente e inequívoco**.
- Los puestos afectados se marcan visualmente con la advertencia: *"Pendiente de sincronización — no mover al personal hasta recuperar la red."*

> **Por qué bloquear en lugar de encolar:** un rechazo digital no deshace un traslado físico. Si el supervisor ya le dijo a alguien que camine a otra línea y la operación se rechaza al volver la red, el sistema y la realidad quedan desincronizados, y nadie se entera hasta que falta una persona. Es preferible impedir la orden que corregirla tarde.

**La aplicación debe funcionar sin depender de recursos externos.** Una terminal sin red debe verse y comportarse igual que una conectada, salvo por las operaciones que requieren el servidor.

**Ningún dato de personal puede salir hacia servicios de terceros.**

## 12.2 Verificación de identidad

Los gafetes se prestan, se confunden y se intercambian. Asignar a la persona equivocada significa que alguien con una restricción médica termina en un puesto que su condición prohíbe.

**Antes de consolidar cualquier registro**, el sistema debe mostrar una confirmación con el nombre completo, el número de ficha, la categoría y **las restricciones médicas activas de forma explícita**. El supervisor confirma deliberadamente: **el escaneo por sí solo nunca asienta la asignación.**

El resultado de cada validación debe comunicarse **con texto y forma, no solo con color**.

**El escáner debe resolver por el número impreso en el gafete físico**, que es el único identificador que la persona lleva encima.

Debe existir **búsqueda manual** por nombre o ficha para gafetes dañados o ilegibles, mostrando **solo personal disponible** y presentando **primero a quienes están físicamente en la línea del supervisor**.

## 12.3 Condiciones de uso reales

- Se opera **con guantes**, con una mano, de pie, en movimiento. Las zonas de toque deben tolerar imprecisión.
- Se opera bajo **iluminación industrial variable**, con la pantalla a brillo parcial y el protector rayado. El contraste debe funcionar en el peor caso.
- Las acciones frecuentes deben estar **al alcance del pulgar**.

## 12.4 Honestidad del sistema

**Ninguna pantalla puede quedar vacía sin explicación.** Todo estado —cargando, sin datos, con error, fuera de operación— tiene representación propia.

**"Cargando" y "vacío" deben distinguirse siempre.** Una línea que aún no responde y una línea sin nadie asignado se ven igual si no se distinguen, y eso lleva al supervisor a reasignar personal que ya estaba colocado.

**Toda acción en curso debe verse y bloquearse contra doble toque.** Sin retroalimentación, el reflejo ante la demora es volver a tocar. Y estas operaciones no son repetibles sin consecuencia: se piden dos relevos, se despacha dos veces, se registra la baja dos veces.

**Todo rechazo explica la causa y el siguiente paso**, en lenguaje de planta. Nunca códigos de error ni mensajes genéricos.

## 12.5 Micro-copia contextual

Cada puesto explica **por qué** está como está:

| Situación | Mensaje |
|---|---|
| Asignado en el barrido automático | *"Asignado automáticamente por asistencia"* |
| Cubierto por un Operador B | *"Cubierto por suplente — titular ausente"* |
| Vacante crítica | *"Sin titular ni suplente disponible"* |
| Fuera de operación | *"Puesto no requerido por el SKU de hoy"* |
| Liberado durante un paro | *"Liberado por paro — operario en ensamble manual L8"* |
| Fatiga sugerida | *"Relevo sugerido — N minutos en el puesto"* |
| Fatiga crítica | *"Límite ergonómico superado — N minutos en el puesto"* |
| Titular reincorporado | *"Titular reincorporado — suplente liberado"* |

## 12.6 Parámetros configurables

Ninguno puede quedar fijo en el código:

- **Orden de prioridad de las 10 líneas** (asignación inicial y extracción inversa)
- **Jerarquía de proximidad física entre líneas** (reasignación del relevado en el motor de relevos)
- Duración de la ventana de arranque local
- Umbrales de fatiga (sugerido y crítico)
- Mínimo de operarios por línea, piso de la extracción inversa
- Umbral de desperdicio que exige justificación
- Umbrales de la escala de eficiencia
- Ritmo teórico de cada SKU
- Categorías y causas de paro

## 12.7 Trazabilidad

Toda operación que mueva a una persona debe quedar registrada: **quién la hizo, cuándo, sobre quién y con qué resultado.** Es lo que permite reconstruir qué pasó cuando algo sale mal, y lo que hace auditable el cumplimiento de las reglas de seguridad ocupacional.

**Además, debe registrarse la hora exacta de salida y de llegada de cada operario en cada movimiento** (despacho, tránsito, recepción, relevo), no solo el resultado final. Es información que hoy no decide nada en el momento, pero **es la materia prima del análisis posterior**: cuánto tarda realmente un traslado entre cada par de líneas, dónde se concentran los cuellos de botella, qué tan bien calibrados están los umbrales de fatiga del §12.6. Sin este dato, cualquier ajuste futuro a esos parámetros se hace a ciegas.
