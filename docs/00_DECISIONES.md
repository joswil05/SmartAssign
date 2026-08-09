# SmartAssign — Registro de Decisiones

**Documento vivo. Es la autoridad sobre toda regla que no estaba cerrada en la especificación funcional.**
Versión 1.0 · 2026-08-08

> **Para qué existe.** La [Especificación Funcional v3.3](fuentes/ESPECIFICACION_FUNCIONAL_SMARTASSIGN.md) es la fuente de verdad del negocio. Cuando un documento técnico necesita una regla que la especificación no cierra, la regla se decide **aquí**, con un identificador estable, y los demás documentos la citan por ese identificador. Ningún documento técnico puede inventar negocio por su cuenta.
>
> Lo que sigue abierto aparece en los demás documentos como `⚠ PENDIENTE-<ID>` y **nunca** redactado como regla firme.

## Cómo leer los estados

| Estado | Significado |
|---|---|
| 🟢 **Cerrada — cliente** | Respondida directamente por el responsable del negocio. Es tan vinculante como la especificación. |
| 🔵 **Cerrada — propuesta aceptada** | La propuso el equipo técnico y el cliente la aprobó. Vinculante. |
| 🟡 **Supuesto declarado** | El equipo técnico procede bajo un supuesto explícito porque el dato no bloquea la construcción. Revisable sin coste alto. |
| 🔴 **Abierta** | Sin resolver. Bloquea o degrada algo. Aparece como `⚠ PENDIENTE` en los documentos afectados. |

## Marcas de regla

| Marca | Significado |
|---|---|
| `[REGLA DURA]` | No cede ante ninguna urgencia, en ningún nivel de ningún motor, para ningún rol. Si un documento técnico la relaja, es un defecto. |
| `[REGLA BLANDA]` | Puede ceder bajo las condiciones que la propia regla declara. |
| `[SEGURIDAD DE DATOS]` | Gobierna qué información ve quién. Su violación es un incidente, no un fallo de interfaz. |

---

# A · Correcciones a la fuente

Estas entradas **corrigen o precisan el texto de la especificación funcional**. Donde haya discrepancia entre la especificación y esta sección, manda esta sección.

## A1 · Jerarquía de proximidad de la L10 — 🟢 Cerrada — cliente

§9.5 publicó la fila de L10 con `L2` repetido y sin `L1`. La fila correcta es:

```
L10 →  L9, L3, L6, L7, L4, L2, L1, L5, L8
```

> **Atención al implementar:** la corrección no es solo sustituir el `L2` duplicado por `L1`. La fuente decía `L3, L9, …` y la fila correcta empieza `L9, L3, …`. **Las dos primeras posiciones también cambian.**

**Tabla de proximidad vigente y completa** (§9.5 corregida). Se lee de más cercana a menos cercana. Es un **grafo dirigido**, no una distancia: ver A3.

| Línea | Recorrido de cercanía |
|---|---|
| L1 | L2, L4, L9, L10, L6, L3, L7, L5, L8 |
| L2 | L4, L1, L7, L9, L10, L3, L6, L5, L8 |
| L3 | L10, L9, L6, L7, L4, L2, L1, L5, L8 |
| L4 | L2, L1, L7, L9, L10, L6, L3, L5, L8 |
| L5 | L1, L2, L4, L7, L9, L10, L6, L3, L8 |
| L6 | L3, L10, L9, L7, L4, L2, L1, L5, L8 |
| L7 | L9, L10, L6, L3, L4, L2, L1, L5, L8 |
| L8 | *No aplica.* La L8 nunca busca "la línea más cercana": es siempre el destino de respaldo. |
| L9 | L3, L10, L6, L7, L4, L2, L1, L5, L8 |
| **L10** | **L9, L3, L6, L7, L4, L2, L1, L5, L8** ← corregida |

*Impacta:* 02 (flujo de relevo), 04 (tabla `ProximidadLinea`), 06 (fase del motor de relevos).

## A2 · El "orden físico de referencia" queda descartado — 🟢 Cerrada — cliente

§9.5 abre con un "orden físico de referencia: L1, L2, L4, L6, L9, L7, L5, L8". Ese dato **se descarta**: solo enumera 8 de las 10 líneas y no es consistente con las filas de la tabla. **La tabla de A1 es la única fuente normativa de proximidad.**

*Impacta:* 04 (no se modela ningún "orden físico" global).

## A3 · La proximidad es asimétrica a propósito — 🟢 Cerrada — cliente

Que L1 sea la más cercana a L5, pero L5 sea la penúltima para L1, **es intencional** y refleja recorridos reales de planta, no distancia euclídea.

**Consecuencia de modelado:** la proximidad se almacena como **grafo dirigido** — 10 filas × 9 posiciones ordenadas, editables por el Coordinador (§12.6). Está prohibido derivarla de una fórmula, de coordenadas o de una matriz simétrica.

*Impacta:* 04 (`ProximidadLinea` con `linea_origen`, `linea_destino`, `orden`), 05 (no hay cálculo geométrico).

## A4 · Fatiga por puesto y alcance real de la regla de 24 horas — 🟢 Cerrada — cliente

Dos precisiones que **cambian el modelo de datos**:

**1. La regla de no repetición de 24 h (§7.4) no es general.** Aplica **únicamente a la actividad "Girar botellas"**. Los demás puestos rotativos no la tienen.

Se modela como atributo del **tipo de actividad**, no como regla global: `TipoActividad.aplica_no_repeticion_24h`. Hoy solo un tipo lo lleva; añadir otro en el futuro es un cambio de dato, no de código.

**2. Cada puesto tiene su propio tiempo de fatiga.** §9.1 daba a entender umbrales globales de planta. No lo son: los umbrales *sugerido* y *crítico* viven **en el puesto**.

Los valores concretos **se calibrarán con datos reales de operación**, así que el sistema se entrega con esos campos vacíos y un valor de planta por defecto como respaldo. El Coordinador los edita (§12.6, §2.1.10).

> **Consecuencia operativa en cascada:** con umbrales por puesto, los minutos absolutos **dejan de ser comparables entre puestos**. Todo ordenamiento por fatiga usa **exceso relativo sobre el umbral propio**, expresado en porcentaje. Ver B3 y B4.

*Impacta:* 04 (`Puesto.umbral_sugerido_min`, `Puesto.umbral_critico_min`, `TipoActividad`), 02 (regla 24 h solo en un puesto), 03 (la barra de fatiga es relativa, no absoluta), 05 (cálculo de fatiga).

## A5 · Orden de extracción inversa — 🟢 Cerrada — cliente

Cuando la L8 se queda sin personal disponible y compatible, la extracción inversa (§9.6) recorre:

```
L10, L9, L3, L5, L7, L6, L2, L1
```

**Derivación:** es la jerarquía de prioridad configurada, invertida, excluyendo la **L8** (está vacía por definición — es lo que dispara el mecanismo) y excluyendo la **línea que solicita**. Se implementa como derivación, no como lista escrita, para que respetar §12.6 (prioridad configurable en caliente) no exija mantener dos listas en sincronía.

> ⚠ **Matiz sin cerrar:** con la configuración base y L4 solicitando, la derivación reproduce exactamente la lista de arriba. Queda por confirmar si L4 está excluida **por ser la solicitante en ese ejemplo** o si **nunca** puede ser donante bajo ninguna circunstancia. Se implementa la primera lectura. Ver `⚠ PENDIENTE-A5b`.

*Impacta:* 04, 05 (motor de extracción inversa), 06.

## A6 · No existe "tiempo mínimo en el puesto" — 🟢 Cerrada — cliente

§2.1.9 mencionaba una restricción de "tiempo mínimo en el puesto" que no aparece en ninguna otra parte del documento. **No es una regla de validación y no existe.**

Lo que sí existe es la **facultad de excepción del Coordinador**: cuando un trabajador acuerda algo directamente con él (permiso, arreglo hablado), el Coordinador puede retirarlo de su puesto y asignar a otra persona, **rellenando un formulario de justificación obligatorio**.

**Entidad nueva que esto crea** — no estaba en la especificación:

`JustificacionExcepcion` = { tipo de excepción, motivo de catálogo, texto libre obligatorio, actor, sello de tiempo, operación afectada }

**Toda excepción del Coordinador la exige. Sin formulario, la operación no se ejecuta.** `[REGLA DURA]`

Excepciones que la requieren: retirar a alguien fuera del flujo normal (§2.1.9), forzar el cierre de turno (C13), saltar la ventana de arranque (B12), extraer un Operador B de otra línea (C15-N3), forzar por debajo del piso de seguridad (B5).

**Lo que la excepción del Coordinador NUNCA salta:** restricciones médicas y compatibilidad de categoría (§2.1.9, textual). `[REGLA DURA]`

*Impacta:* 04 (tabla `JustificacionExcepcion`), 02 (formulario en cada flujo de excepción), 01 (historias del Coordinador).

## A7 · Movilidad real del Operador B y del Operador C — 🟢 Cerrada — cliente

Precisión sobre §4.1 y §4.2:

- **Operador B:** mientras el Operador A titular **está presente**, el B trabaja en **puestos rotativos** ("entrepuestos varios"). Solo pasa a puesto fijo cuando debe sustituir al A. Su estado normal es rotativo, no banca.
- **Operador C:** puede estar en puesto **rotativo** o en puesto **fijo en entrenamiento** junto al Operador A.

**Consecuencia lógica derivada, vinculante:**

> **La fatiga es propiedad del puesto ocupado, no de la categoría de la persona.** Cualquiera que ocupe un puesto rotativo acumula fatiga y puede ser relevado — operario, Operador B u Operador C. Los puestos fijos no acumulan (§5.1), los ocupe quien los ocupe.

*Impacta:* 04 (la fatiga cuelga de la asignación, no del trabajador), 02 (el B aparece en las colas de relevo), 05.

## A7b · Personal de liderazgo y matriz de compatibilidad — 🔵 Cerrada — propuesta aceptada

§4.1 permite asignar manualmente a personal de liderazgo en déficit crítico, pero §4.2 no le da casilla en ninguna fila de la matriz.

**Resolución:** la asignación manual de liderazgo **sí salta la matriz de categoría**, y **solo** bajo estas condiciones acumulativas:

1. Es un **acto deliberado de dos pasos** (§4.1): seleccionar primero el puesto destino, registrar después a la persona. Nunca por sugerencia del motor.
2. Ningún motor automático propone jamás a personal de liderazgo (§4.1, textual).
3. Requiere justificación registrada (A6).
4. **No salta restricciones médicas.** `[REGLA DURA]`

*Impacta:* 04 (excepción explícita en la validación de categoría), 02.

## A8 · No existe categoría de regla por sexo ni "restricción técnica" — 🟢 Cerrada — cliente

Verificado por búsqueda sobre la v3.3: los términos "sexo" y "género" **no aparecen en el documento**. El ejemplo del §9.4 es normativo tal cual está escrito.

Los únicos motivos por los que la L8 puede no tener candidato para un puesto son: **restricciones médicas**, **incompatibilidad de categoría**, o **no tener más gente disponible en ese momento**.

**No se crea ninguna categoría de regla adicional.** El único contenedor de preferencias blandas sigue siendo el *perfil preferente* (§7.3).

*Impacta:* 04 (no hay tabla de restricciones por sexo), 01, 02.

## A9 · Separación estricta entre prioridad y proximidad — 🟢 Cerrada — cliente

> **El motor de relevos se rige únicamente por proximidad de línea y compatibilidad de puestos. La jerarquía de prioridad de líneas solo aplica a la asignación inicial de personal.**

Reconciliación con A5, porque parecen chocar y no chocan:

| Mecanismo | Criterio | Referencia |
|---|---|---|
| **Motor de asignación inicial** | Jerarquía de **prioridad**, de mayor a menor | §3.3, §8.3 |
| **Motor de relevos** (a quién relevar, a dónde va el relevado) | **Proximidad** + compatibilidad. Nunca prioridad. | §9.4, §9.5, A1 |
| **Extracción inversa** (emergencia: la L8 está vacía) | Prioridad **invertida** | §9.6, A5 |

Son **tres mecanismos distintos**. La extracción inversa no es parte del motor de relevos: es un mecanismo de emergencia que se dispara cuando el motor de relevos se queda sin insumo.

> **Consecuencia de diseño vinculante:** implementarlos como un solo motor parametrizado es un error. Se acaba filtrando la prioridad a decisiones de relevo, que es exactamente lo que esta decisión prohíbe. Van en **tres servicios separados** (ver 05).

*Impacta:* 05, 04, 06.

## A9b · Vigencia del anexo de arquitectura — 🟡 Supuesto declarado

El anexo se declara complementario a la v3.0 y la especificación vigente es la v3.3. **Se procede asumiendo que sigue vigente**: solo prescribe plataforma (Android nativo, SQL Server, API intermedia obligatoria) y nada de eso fue tocado entre v3.0 y v3.3.

---

# B · Reglas de motor

## B1 · Desempate de concurrencia — 🔵 Cerrada `[REGLA DURA]`

§7.5 exige que "gane uno" sin definir cuál.

> **Gana la primera transacción que confirma en el servidor.** No el primer toque en pantalla, no el rol, no la prioridad de la línea.

Mecanismo: control de concurrencia optimista con `rowversion` sobre el trabajador, más índice único filtrado que impide dos asignaciones activas de la misma persona. La operación es atómica: nunca queda el puesto ocupado y la persona libre, ni al revés (§7.5).

El perdedor recibe un rechazo nominal, no genérico (§12.4):

> *"[Nombre] acaba de ser registrado en L4 · Puesto 3 por otro supervisor."*

**Por qué así:** cualquier otro criterio haría perder al supervisor que tiene a la persona físicamente enfrente contra uno que está al otro extremo de la planta. El servidor es el único árbitro (§7, encabezado).

*Impacta:* 04 (concurrencia), 05 (transacciones), 02 (pantalla de rechazo).

## B2 · Ranking del candidato que la L8 propone — 🔵 Cerrada

§9.4 paso 2 pide "el candidato compatible más apto" sin definir apto. Entre el personal de la L8 disponible, compatible (§4.2), sin restricción médica que lo impida (§7.2) y **no descartado** para ese puesto (B10), se ordena:

| Orden | Criterio | Por qué |
|---|---|---|
| 1 | Es **titular/habitual** del puesto destino | Espeja §8.5 nivel 1: la menor fricción posible, ya conoce la tarea |
| 2 | **Más tiempo en el Bolsón** | Justo, y es quien menos está aportando en ese momento |
| 3 | **Menor fatiga acumulada** en la jornada | Reparte el desgaste |
| 4 | Ficha ascendente | Desempate **estable** |

**El perfil preferente (§7.3) ordena, no excluye.** Quien lo cumple rankea por encima de quien no. Un candidato que solo falla el perfil **sí se propone si no hay otro**, marcado explícitamente como tal — espeja la escalera del §8.5.

> **Por qué el desempate estable importa:** la misma situación debe producir siempre la misma sugerencia. Un motor que propone a alguien distinto cada vez que se refresca la pantalla parece errático, y §9.3 advierte que un motor en el que no se confía se deja de usar.

*Impacta:* 05, 02, 06.

## B3 · Orden de la cola de relevos pendientes en la L8 — 🔵 Cerrada

Por A9, **no se ordena por prioridad de línea**.

| Orden | Criterio |
|---|---|
| 1 | Nivel **crítico** antes que **sugerido** (§9.1) |
| 2 | Mayor **exceso relativo sobre su propio umbral**, en % |
| 3 | **FIFO** por antigüedad de la solicitud |

El *relevo solicitado* manual (§9.4 paso 1) entra al nivel de *sugerido*, salvo que el puesto ya sea crítico por su propio reloj.

**Excepción de máxima prioridad:** una solicitud generada por vacante crítica de puesto fijo (C15-N1) encabeza la cola por delante de cualquier fatiga.

> **Por qué exceso relativo y no minutos:** con umbrales por puesto (A4), 70 minutos en un puesto cuyo umbral es 60 es peor que 70 minutos en uno cuyo umbral es 120. Ordenar por minutos absolutos atendería primero al que menos lo necesita.

*Impacta:* 05, 02, 03.

## B4 · Destino del relevado — 🔵 Cerrada

§9.4 paso 6 pide "el puesto fatigado más conveniente". Se resuelve:

1. **Misma línea primero.** Entre los puestos fatigados compatibles de su línea, el de **mayor exceso relativo** (B3).
2. Si no hay ninguno, recorre la **fila de proximidad de A1** línea por línea; en la primera línea que tenga un puesto fatigado compatible, otra vez el de mayor exceso relativo.
3. Si no hay ninguno en todo el recorrido, **L8**.

**Guarda obligatoria:** el puesto destino **no puede estar ya reservado** por otro relevista en tránsito. Sin esta guarda, dos personas convergen al mismo puesto y una queda sin destino a mitad de la planta.

> **Por qué no se usa distancia dentro de la línea:** el modelo no tiene geometría intra-línea y no se inventa. Dentro de una línea, todos los puestos se consideran igual de cercanos.

*Impacta:* 05, 04 (reserva), 02.

## B5 · Piso de seguridad por línea — 🔵 Cerrada `[REGLA DURA]`

El mínimo de operarios del §9.6 es **configurable por línea**, con un valor de planta por defecto: `Linea.minimo_operarios` nulable; `NULL` → default global.

- Cuenta **operarios en puestos rotativos ocupados**. Los puestos fijos no cuentan: no se extraen nunca (§5.1, §11.1).
- Al alcanzar el mínimo, la línea queda **inmune a extracción**.
- Si todas las líneas candidatas están en el mínimo, **se detienen las rotaciones** y se alerta al Coordinador con el texto del §9.6: *"Capacidad crítica de planta agotada. Requiere intervención humana."*
- El Coordinador puede forzar por debajo del piso **solo con justificación** (A6).

> **Por qué por línea y no global:** una línea de 6 puestos rotativos y una de 20 no pueden compartir el mismo piso. Un valor único o desmantela la pequeña o congela la grande.

*Impacta:* 04, 05, 02.

## B6 · Ventana temporal de la regla de 24 horas — 🔵 Cerrada `[REGLA DURA]`

Alcance ya cerrado en A4: solo la actividad "Girar botellas".

**Ventana:** se deniega si la persona ocupó un puesto de esa actividad en su **jornada trabajada anterior**, no en el día calendario anterior.

> **Por qué la jornada y no el calendario:** con día calendario, tres días de descanso limpiarían la regla en silencio y alguien volvería al mismo puesto desgastante sin que nada lo señalara. Es una regla ergonómica; la lectura estricta es la segura.

La referencia se persiste al **cierre de turno** (C13): el último puesto ocupado por cada persona.

*Impacta:* 04 (`UltimaTareaJornada`), 05, 02.

## B7 · Efecto del doble turno — 🔵 Cerrada

§11.5 exige que sea visible pero no define si ajusta algo. Resolución de dos partes:

1. **Distintivo visual permanente** en la persona, en toda pantalla donde aparezca (§11.5).
2. **Factor de doble turno configurable** sobre el reloj de fatiga, **con valor por defecto `1.0`**.

Con el factor en 1.0 el comportamiento es puramente informativo, que es lo correcto al lanzar: los umbrales reales aún no están calibrados (A4). Cuando haya datos, subir el factor acelera la aparición de *sugerido* y *crítico* para esa persona **sin tocar código**.

**Nunca bloquea.** Una persona en doble turno sigue siendo asignable; el sistema solo señala antes.

*Impacta:* 04 (`Parametro.factor_doble_turno`), 05, 03.

## B8 · Cambio de jerarquía de prioridad en caliente — 🔵 Cerrada

**Solo hacia adelante. Nunca retroactivo.**

- Afecta a la **siguiente** corrida del motor de asignación inicial.
- Afecta al orden de **extracción inversa** (A5) desde ese instante.
- **No revisa ninguna asignación vigente.** Nadie se mueve por un cambio de configuración.
- El cambio queda auditado: quién, cuándo, valor anterior y nuevo (§12.7).

> **Por qué:** un recálculo retroactivo o mueve personal por su cuenta —prohibido por §9.3— o vomita decenas de sugerencias simultáneas a mitad de turno. Además A9 acota la prioridad a la asignación inicial, así que a mitad de turno hay muy poco sobre lo que actuar.

*Impacta:* 05, 02, 01.

## B9 · Qué hace el nivel crítico — 🔵 Cerrada

§9.1 define tres niveles pero §9.4 solo describe qué dispara *sugerido*. El nivel **crítico** hace tres cosas, **ninguna automática sobre personas**:

1. **Salta al frente** de la cola de la L8 (B3).
2. **Re-notifica una sola vez** a todos los supervisores al cruzar el umbral. Una sola vez, no en bucle.
3. Si permanece crítico más de un tiempo configurable **sin relevo aceptado**, escala como alerta al **Coordinador**, el único con poder transversal (§2.1.8).

> **Por qué una sola re-notificación:** notificar en bucle es ruido, y el ruido entrena a ignorar. El escalado va al único que puede desbloquearlo.

*Impacta:* 05, 02, 03.

## B10 · Ciclo de vida de la lista de descartados — 🔵 Cerrada

§9.4 paso 3 exige que se pueda limpiar, sin decir quién ni cuándo.

- El descarte es del par **(puesto, persona)**, no de la persona en general.
- Lo crea **solo el supervisor de la L8**, al rechazar una propuesta.
- **Caduca automáticamente al cierre de turno.** Un rechazo es contextual al momento.
- Dentro del turno lo limpian el **supervisor de la L8** (que lo creó) o el **Coordinador**. El supervisor destino **no**: no manda sobre personal ajeno (§2.2).
- La lista es **visible** con su conteo, para que nadie quede vetado en silencio.

> **Por qué la caducidad:** §9.4 advierte contra "un veto permanente e invisible". Depender de que alguien se acuerde de limpiar la lista es exactamente cómo se llega a ese veto.

*Impacta:* 04 (`RelevoDescartado`), 05, 02, 03.

## B11 · Caducidad de tránsito y de reserva — 🔵 Cerrada

Hoy una persona en tránsito es inmune (§6.1) y su puesto destino queda reservado (§9.4 paso 3) **sin ninguna salida definida**. Si nunca llega, queda congelada y el puesto bloqueado.

- Parámetro `duracion_maxima_transito`, configurable. **Valor inicial provisional: 15 minutos**, a calibrar con los datos de salida/llegada del §12.7 — que es exactamente para lo que ese registro existe.
- Al vencer, el tránsito **no se resuelve solo**. Se emite alerta al supervisor de origen, al de destino y al Coordinador.
- El puesto sigue reservado, marcado *"Relevista demorado"*.
- **Cancelar es acto humano:** el supervisor destino puede liberar la reserva; el Coordinador puede cancelar el tránsito, y entonces la persona pasa a *presente, sin asignar* en su última línea física conocida.

> **Por qué no se resuelve solo:** resolver automáticamente sería mover a alguien sin decisión humana (§9.3). Pero dejar el estado sin salida es peor. La caducidad convierte un cuelgue silencioso en una alerta accionable, sin mover a nadie.

*Impacta:* 04, 05, 02, 03.

## B12 · Qué cede y qué no — 🔵 Cerrada `[REGLA DURA]`

**Tabla de autoridad. Si un documento técnico la contradice, el documento técnico está mal.**

| Regla | ¿Cede? | Condición |
|---|---|---|
| Restricciones médicas (§7.2) | **NUNCA** | Ningún nivel, ningún motor, ningún rol, ninguna urgencia. Ni el Coordinador (§2.1.9). |
| Compatibilidad de categoría (§4.2) | **NUNCA** | Ni el Coordinador (§2.1.9, textual). Única salvedad: A7b, liderazgo por acto deliberado de dos pasos con justificación. |
| No repetición 24 h (§7.4, A4) | **NUNCA** | Tan acotada tras A4 (un solo puesto) que jamás bloqueará la planta. No necesita válvula de escape. |
| Puesto libre / persona disponible (§7.1 pasos 1–2) | **NUNCA** | Son condiciones de integridad, no preferencias. |
| **Perfil preferente (§7.3)** | **SÍ** | Es lo único que cede. Niveles 2 y 4 de la escalera §8.5, y ordenación en B2. Si el dato de la persona no está registrado, la regla **no se aplica** — nunca se infiere (§7.3). |
| Ventana de arranque (§8.4) | **Solo Coordinador** | Con justificación (A6). |
| Piso de seguridad (§9.6, B5) | **Solo Coordinador** | Con justificación (A6). |

*Impacta:* todos.

---

# C · Flujos y estados

## C1 · Reincorporación del titular — 🔵 Cerrada

§12.5 tiene la micro-copia *"Titular reincorporado — suplente liberado"* pero el flujo no existía en ninguna parte.

1. **Lo dispara el supervisor** al registrar al titular que llega. No es automático: el titular puede llegar y pasar a enfermería.
2. El sistema detecta que su puesto fijo está cubierto por un suplente y ofrece **"Devolver puesto al titular"**, con la micro-copia del §12.5.
3. **El supervisor puede declinar** y dejar al suplente. Conoce contexto que el sistema no ve (§9.3).
4. Si acepta, el **Operador B liberado no va a la L8**: entra en la misma lógica del §9.4 paso 6 → puesto rotativo fatigado de su propia línea → proximidad → L8.

> **Por qué el B no va al Bolsón:** por A7, con el titular presente el estado normal del Operador B es ocupar un rotativo. Mandarlo a la L8 desperdicia el recurso más escaso de la planta (§4.1).

*Impacta:* 02, 04, 05, 03.

## C2 · Salida del retiro temporal — 🔵 Cerrada

§6 lista *retirado temporalmente*, §9.7 lo crea, nadie lo cerraba.

- Solo lo reincorpora el **Coordinador**. El retiro es médico o de contingencia y §2.1.6 pone lo médico bajo su responsabilidad (C14).
- La persona pasa a *presente, sin asignar* en su línea física; el supervisor la registra normalmente.
- Queda auditado (§12.7).

*Impacta:* 02, 04, 01.

## C3 · Origen de la presencia y de la línea física — 🔵 Cerrada

§8.2 exige saber en qué línea está físicamente cada persona, pero ningún rol tiene la capacidad de marcar entrada.

- **Sin integración de reloj checador en el MVP.** Queda como punto de extensión documentado.
- La línea física inicial sale de `Personal.linea_habitual`, que §8.2 ya presupone ("deducido de dónde trabajó habitualmente") y que §8.5 nivel 1 necesita.
- La transición *fuera de turno → presente, sin asignar* ocurre **implícitamente cuando un supervisor lo registra**, o explícitamente si el Coordinador marca presencia en el padrón.

> **Por qué es suficiente:** §6.1 ya establece que "quien está fuera de turno SÍ puede ser asignado". El marcaje nunca fue un requisito previo. Inventar una integración de asistencia que nadie especificó sería peor que no tenerla.

*Impacta:* 04 (`linea_habitual`, `linea_fisica_actual`), 02, 01 (fuera de alcance).

## C4 · Captura de producción y estadística viva — 🔵 Cerrada, ampliada por el cliente

§11.4 exige mostrar eficiencia en tiempo real pero nadie capturaba la producción real.

**Captura:**
- El supervisor registra la producción **al cierre de lote**, junto al desperdicio (§11.3). Mismo momento, mismo actor.
- Para la lectura en vivo, registra **avances parciales** con un contador simple durante el lote.
- Mientras no haya registro nuevo, la pantalla dice *"estimada desde el último registro — hace N min"*. **Nunca un número inventado** (§12.4).

**Estadística viva — requisito ampliado por el cliente:**

> **Todo registro que hace el supervisor — paros, desperdicio, producción, movimientos de personal — debe reflejarse siempre en las estadísticas calculadas que se visualizan en su propio panel y en el del Coordinador.**

Cómo se garantiza:

1. Cada operación escribe en su tabla **y emite un evento** por el canal de tiempo real, dentro de la misma transacción.
2. El servidor recalcula los indicadores de la línea afectada y los **empuja** al supervisor de esa línea y al Coordinador.
3. **El cálculo vive en el servidor, nunca en el dispositivo.** Igual que la decisión no puede vivir en el dispositivo (§7), el número tampoco: dos paneles calculando por su cuenta acaban mostrando cifras distintas del mismo turno.
4. Todo indicador lleva **sello de última actualización** (§12.4, D4).

**Indicadores del panel de supervisor:** eficiencia de su línea (§11.4), tiempo de paro acumulado del turno, desperdicio del lote por causa, cobertura de la línea, puestos en fatiga.

**Indicadores del panel de Coordinador:** los mismos por las 10 líneas, más el agregado de planta.

Integración con MES/SCADA = punto de extensión, fuera del MVP.

*Impacta:* 04, 05 (servicio de estadística + canal de tiempo real), 02, 03, 01.

## C5 · Definición de lote — 🔵 Cerrada

No estaba definido pese a que §11.3 y §3.1 lo usan.

`Lote` = ( línea, SKU, turno, apertura, cierre )

- **Abre** cuando la línea empieza a producir ese SKU: al arrancar el turno, o tras un cambio de SKU (§11.2).
- **Cierra** con la acción explícita *"Cerrar lote"*, que es donde se capturan desperdicio (§11.3) y producción (C4).
- **Varios lotes por turno** están permitidos.
- **Cambio de SKU** = cierre de lote + apertura de lote nuevo, pasando la línea por *En limpieza* (§3.1).
- **Cierre de turno** fuerza el cierre del lote abierto exigiendo la misma captura (C13).

*Impacta:* 04, 02, 05.

## C6 · Turnos y "día de operación" — 🔵 Cerrada

- Catálogo `Turno` ( nombre, hora inicio, hora fin, cruza medianoche ).
- **Día de operación** = fecha de inicio del turno. Un turno que cruza medianoche pertenece **entero** a su fecha de inicio.
- Todo lo que la especificación llama "jornada anterior" (§7.4, B6) usa **jornada**, no fecha calendario.
- **La hora es siempre la del servidor.** El reloj del dispositivo no se usa para ninguna decisión, ningún cálculo de fatiga y ningún sello de auditoría.

> **Por qué la hora del servidor:** el reloj del teléfono se desajusta y se puede manipular. §7 establece que la decisión final nunca vive en el dispositivo; un cálculo de fatiga basado en el reloj local sería exactamente eso.

Los horarios concretos se siembran vacíos: son dato de configuración del cliente.

*Impacta:* 04, 05, 02.

## C7 · Modelo de la Línea 8 — 🔵 Cerrada

La L8 **es una línea** en el modelo (tiene supervisor, ocupa posición en la jerarquía), con rasgos propios:

| Aspecto | Resolución |
|---|---|
| Mesas de ensamble manual | **No se modelan como puestos.** §6 distingue *En Bolsón* de *Asignado*: estar en el Bolsón es estar en la línea, disponible y produciendo, sin ocupar un puesto identificado. |
| Fatiga en el Bolsón | **No acumula.** No genera relevos. |
| Puestos fijos en L8 | El modelo los **permite** por si tiene máquinas; se siembra vacío. |
| SKU, eficiencia, desperdicio, paro | **Fuera del MVP.** La fórmula del §11.4 exige un ritmo teórico de SKU que la L8 no tiene. |
| Pantalla principal de su supervisor | **Distinta.** Lista de personal disponible + cola de solicitudes de relevo + recepciones pendientes. **No una malla de puestos.** |

*Impacta:* 04, 02, 03, 01.

## C8 · Recepción en la L8 — 🟢 Cerrada — cliente

Un paro (§11.1), un cambio de SKU (§11.2) o una línea inactiva (§8.1) pueden mandar una docena de personas a la L8 de golpe.

> **La confirmación de recepción es individual, persona por persona.** No hay confirmación en bloque.

Cada persona genera su propio tránsito con su hora de salida y de llegada (§12.7).

**Mitigación de fricción — es problema de interfaz, no de regla.** El supervisor de la L8 tiene una pantalla *Recepciones pendientes* con:
- lista de tarjetas grandes, **una sola pulsación por persona**, sin modal intermedio;
- contador visible de lo que falta;
- nombre y ficha en la tarjeta, suficientes para verificar quién llegó.

**Proporcionalidad de la verificación de identidad:** recibir en el Bolsón no asigna ningún puesto, así que basta la tarjeta con nombre y ficha. La confirmación completa del §12.2 —con categoría y restricciones médicas explícitas— se exige **al asignar a un puesto**, que es cuando una identidad equivocada tiene consecuencia ocupacional. `[REGLA DURA]` en el segundo caso.

*Impacta:* 02, 03, 04.

## C9 · Paro con un relevista en tránsito hacia esa línea — 🔵 Cerrada

- El tránsito **no se cancela**: es inmune (§6.1).
- Al llegar, el supervisor destino ve el estado explícito: *"L4 está en paro — el puesto que venía a cubrir fue liberado"*.
- Única acción disponible: **despacharla a la L8**. Si el paro ya terminó, se asigna normalmente.
- La reserva del puesto fatigado se libera cuando el paro vacía los rotativos (§11.1).

*Impacta:* 02, 05, 03.

## C10 · Rechazo de recepción — 🔵 Cerrada

§9.4 paso 5 y Parte X paso 3 permiten rechazar, devolviendo la persona "a la L8", sin decir en qué estado.

1. La persona queda **en tránsito hacia L8**, no directamente *En Bolsón*: está físicamente en la línea destino y tiene que caminar. §12.7 necesita las dos horas.
2. El puesto vuelve a la cola de relevos pendientes **con su nivel de fatiga actual**.
3. La persona **entra en la lista de descartados de ese puesto** (B10): el rechazo fue específico de ese emparejamiento.
4. El rechazo **exige motivo**: lista corta configurable + texto opcional. Queda auditado.

> **Por qué el motivo es obligatorio:** sin él, rechazar recepciones se convierte en un canal silencioso para esquivar relevos, y nadie puede detectarlo después.

*Impacta:* 02, 04, 05.

## C11 · Rotativo descubierto ≠ vacante crítica — 🔵 Cerrada

- **Vacante crítica** (§5.3) queda **exclusiva de puestos fijos**. Su definición —"ni titular ni suplente"— es lenguaje de puestos fijos y su prominencia máxima corresponde a una máquina sin operador.
- Para un puesto rotativo descubierto durante la operación se crea un estado distinto: **"Rotativo descubierto"**. Cuenta en el déficit, con prominencia menor.

> **Por qué separarlos:** un rotativo puede estar legítimamente vacío justo tras un paro o un cambio de SKU. Igualarlo a una máquina sin operador banaliza la alerta que sí importa. Nombre distinto obliga a estado visual distinto (§12.4).

*Impacta:* 03, 04, 02.

## C12 · Titularidad en puestos rotativos — 🔵 Cerrada

§5.1 atribuye titular solo a los fijos, pero §8.5 nivel 1 lo presupone en rotativos.

`Puesto.titular_id` existe en **ambos tipos**, con semántica distinta:

| Tipo | Semántica del titular |
|---|---|
| **Fijo** | Asignación técnica. **Dispara el barrido automático** del §8.3. |
| **Rotativo** | **Mera preferencia.** Alimenta la escalera del §8.5 nivel 1. **Nunca genera asignación automática.** |

> **Por qué la distinción explícita:** sin ella, el barrido del §8.3 llenaría los rotativos al arrancar, y §5.2 exige que empiecen vacíos.

*Impacta:* 04, 05, 02.

## C13 · Cierre de turno — 🔵 Cerrada

**Bloqueado** mientras haya:
- un lote abierto;
- personas en tránsito **hacia** su línea;
- personas suyas en tránsito **hacia fuera** aún no recibidas.

El sistema **lista exactamente qué lo bloquea y a quién llamar** (§1.3, §12.4). Nunca un rechazo genérico.

**Al cerrar:**
- su personal asignado pasa a *fuera de turno*;
- se persiste el **último puesto ocupado por persona**, referencia de la regla de 24 h (B6);
- se cancelan los relevos pendientes de su línea;
- se liberan los puestos fijos;
- caducan los descartados de sus puestos (B10).

El Coordinador puede **forzar** el cierre con justificación (A6). El cierre de planta es acción suya.

> **Por qué bloquear:** cerrar con gente en tránsito deja personas caminando hacia una línea que ya no las espera. Es exactamente el problema del §1.1 —"entre que sale de una línea y llega a otra, desaparece del control"— reintroducido por la puerta de atrás.

*Impacta:* 02, 05, 04.

## C14 · Restricciones médicas: autoría y vigencia — 🔵 Cerrada `[SEGURIDAD DE DATOS]`

- **Enfermería no es usuario del sistema en el MVP.** El Coordinador transcribe (§2.1.6). El registro guarda `fuente` y `fecha_dictamen` para que el origen en papel sea rastreable. Rol de enfermería = punto de extensión.
- Las restricciones tienen **vigencia**: `fecha_inicio` y `fecha_fin` nulable (`NULL` = permanente). §7.2 evalúa **solo las activas hoy**.
- **Nunca se borran.** Solo se cierran con fecha de fin.

> **Por qué no se borran:** eliminar historial médico rompe la auditabilidad de una regla de seguridad ocupacional (§12.7). Y sin vigencia, una restricción temporal por una lesión ya curada bloquearía a esa persona para siempre.

*Impacta:* 04, 05, 02, 01.

## C15 · Vacante crítica de puesto fijo en operación — 🟢 Cerrada — cliente

Situación que la especificación no cubría: el titular Operador A se retira (§9.7) con el turno ya arrancado. El barrido del §8.3 ya corrió y no vuelve a correr. La máquina queda sin operador.

**Regla del cliente:**

> **Si hay déficit de Operador A, el que le sigue es el Operador B. Sin importar dónde esté, debe ser asignado al puesto — y se debe ejecutar la rotación, porque dejará un puesto vacío.**

**Escalera de cobertura:**

| Nivel | Origen del Operador B | Quién ejecuta | Hueco que deja |
|---|---|---|---|
| **N1** | Disponible en el **Bolsón (L8)** | Supervisor de L8, por flujo de relevo estándar con **máxima prioridad en la cola** (B3) | Ninguno |
| **N2** | En puesto **rotativo de la misma línea** | **Supervisor de la línea afectada** | Rotativo descubierto (C11) en su propia línea |
| **N3** | En puesto **rotativo de otra línea** | **Coordinador**, con justificación (A6) | Rotativo descubierto en la línea de origen |
| **N4** | No hay ningún Operador B en planta | — | Vacante crítica persistente + alerta al Coordinador |

**N3 recorre la jerarquía de proximidad (A1) desde la línea afectada**, y se notifica al supervisor de origen. El movimiento se ejecuta bajo despacho/tránsito/recepción (Parte X).

### Excepción declarada a hub-and-spoke

N3 es una transferencia directa entre dos líneas activas, que §3.2 prohíbe. Se declara como **excepción nombrada: *extracción de Operador B por vacante crítica***, y queda acotada:

- solo para cubrir una **vacante crítica de puesto fijo**;
- solo con un **Operador B**;
- solo la ejecuta el **Coordinador**, con justificación (A6).

**Justificación,** con el mismo razonamiento que la excepción del propio §3.2: obligar a pasar por la L8 duplica el recorrido físico mientras una máquina está parada, y no evita ningún dominó — el hueco que se abre es el mismo por las dos rutas.

### Guarda anti-dominó

> El puesto rotativo que queda vacío en N2 y N3 entra a la cola de relevos pendientes **a prioridad normal**, no como una emergencia nueva.

Así la cadena **se detiene en un nivel**: una vacante crítica genera un rotativo descubierto, y ese rotativo descubierto se cubre por el flujo ordinario. No dispara una segunda extracción de emergencia. Sin esta guarda, la regla de C15 reabre exactamente el efecto dominó que §3.2 existe para impedir.

### Piso de seguridad

B5 aplica a N2 y N3: no se extrae de una línea que quedaría por debajo de su mínimo. Si todas las líneas candidatas están en el piso, no se extrae — se alerta al Coordinador, que puede forzar con justificación (A6).

*Impacta:* 02 (flujo nuevo), 04, 05, 01, 03, 06.

---

# D · Seguridad y aislamiento

## D1 · Qué ve la L8 de un puesto ajeno — 🔵 Cerrada `[SEGURIDAD DE DATOS]`

§2.2 declara aislamiento total; §9.4 paso 2 obliga a la L8 a operar sobre puestos de otras líneas. Se resuelve exponiendo el **mínimo estricto**.

**Lo que la L8 ve de un puesto de otra línea:**
- línea e identificador del puesto;
- tipo de puesto;
- nivel de fatiga y exceso relativo;
- **capacidades físicas que el puesto exige** (necesarias para entender por qué un candidato es o no compatible);
- perfil preferente, si el puesto lo declara.

**Lo que la L8 NUNCA ve:**
- nombre, ficha ni foto del operario que va a ser relevado;
- **restricciones médicas de personal ajeno**;
- ningún otro dato del personal de otra línea.

**El emparejamiento lo hace el servidor.** La L8 recibe el resultado, no los insumos. Ve datos médicos **solo de su propio personal**, que es lo que §12.2 le exige para confirmar identidad.

*Impacta:* 04 (vistas y autorización), 05, 02, 03.

## D2 · Contenido del aviso de fatiga a todos los supervisores — 🔵 Cerrada `[SEGURIDAD DE DATOS]`

§9.4 paso 1 notifica a **todos** los supervisores; §2.2 prohíbe ver personal ajeno.

**Contenido exacto del aviso:**

> `L4 · Puesto 3 — relevo sugerido · 62 min`

**Ninguna identidad de persona**, ni en el aviso ni al abrirlo. Un supervisor que toca el aviso no obtiene más detalle que línea, puesto y nivel.

> **Por qué basta:** §9.4 declara que el propósito es "que sepan lo que pasa en la planta aunque no puedan actuar sobre ello". La conciencia de situación necesita el lugar, no la persona.

*Impacta:* 05, 02, 03.

## D3 · Datos médicos en el dispositivo — 🔵 Cerrada `[SEGURIDAD DE DATOS]`

§12.1 exige que una terminal sin red se vea y se comporte igual que una conectada. §12.2 hace de mostrar las restricciones médicas activas un requisito **previo** a consolidar cualquier registro. Sin caché local, lo primero que se rompe sin red es justamente la pantalla de seguridad.

**Se permite la caché, acotada y cifrada:**

| Dimensión | Regla |
|---|---|
| **Alcance** | Solo el personal **de su línea** más los **físicamente presentes en ella** — el mismo conjunto que §12.2 exige en la búsqueda manual. **Nunca el padrón completo.** |
| **Cifrado** | Base local cifrada, clave en Android Keystore. **Jamás** en preferencias, ficheros planos ni logs. |
| **Purga** | Al cerrar sesión, al cerrar turno, al reasignar línea, y por inactividad configurable. |
| **Coordinador** | Su dispositivo **no cachea restricciones médicas** de las 10 líneas: las consulta en línea bajo demanda. |

*Impacta:* 05, 04, 03, 02.

## D4 · Antigüedad de los datos mostrados sin red — 🔵 Cerrada

- Cada pantalla lleva **sello de frescura**: *"Datos de hace N min"*.
- Bajo `antiguedad_maxima` (**valor inicial provisional: 5 min**) se muestra discreto.
- Por encima: **banner permanente** y los datos se degradan visualmente, con la leyenda del §12.1 — *"Pendiente de sincronización — no mover al personal hasta recuperar la red."*
- **Nunca se presenta dato viejo como si fuera vivo** (§12.4). `[REGLA DURA]`

*Impacta:* 03, 02, 05.

## D5 · Notificaciones con la app cerrada — 🟢 Requisito del cliente · arquitectura 🔵 propuesta

> **Requisito del cliente, calificado de vital: las notificaciones deben llegar a supervisores y Coordinador aunque no tengan la app abierta.**

Y §12.1 prohíbe que ningún dato de personal salga hacia servicios de terceros. Se resuelve con una arquitectura de tres capas **enteramente dentro de la red de planta**:

**Capa 1 — Canal.** Servicio en primer plano (`FOREGROUND_SERVICE_DATA_SYNC`) con notificación persistente, manteniendo una conexión permanente contra el servidor de planta. Sobrevive a que la app no esté en primer plano y a que se cierre desde el conmutador de aplicaciones.

**Capa 2 — Resiliencia.** Arranque en `BOOT_COMPLETED`; exención de optimización de batería; watchdog con alarma exacta que verifica que el servicio vive y lo reinicia; reconexión con retroceso exponencial.

**Capa 3 — Garantía de entrega.** El servidor marca cada notificación como **entregada o no entregada**. Si una notificación crítica no se acusa en un tiempo configurable, **escala al Coordinador** y aparece en su panel como *"supervisor no localizable"*.

> **Por qué la capa 3 es la que de verdad cumple el requisito:** ninguna app puede garantizar entrega en Android al 100 %. Lo que sí se puede garantizar es que **nadie crea que se notificó cuando no se notificó**. La capa 3 convierte un fallo silencioso en información operativa, que es la misma lógica del §1.3 y del §12.4.

### ⚠ Dependencia dura: MDM / Device Owner

Un *force-stop* del usuario, o las políticas agresivas de ciertos fabricantes, matan el servicio, y **ninguna aplicación Android puede evitarlo sin política de dispositivo**. Por tanto:

> **MDM / Device Owner asciende de conveniencia de despliegue a requisito de arquitectura.** Con Device Owner se bloquea el force-stop, se fija la exención de batería por política y se garantiza el arranque tras reinicio.

**Si no hay MDM**, la única vía que entrega con la app forzada a cerrarse es FCM. En ese caso la propuesta sería **FCM como campana vacía**: carga útil literalmente sin contenido, la app despierta y consulta al servidor de planta por HTTPS. Ningún dato de personal sale; lo único que sale es el token del dispositivo. **Es una decisión del cliente, no del equipo técnico.** Ver `⚠ PENDIENTE-E2`.

*Impacta:* 05 (arquitectura completa), 04 (acuse de entrega), 06 (fase de notificaciones), 02.

## D6 · Autenticación y sesión — 🔵 Cerrada

Ninguna fuente decía nada. Se define:

- **JWT** emitido por el API: acceso de vida corta (~15 min) + refresh ligado al dispositivo.
- Credenciales validadas contra **Active Directory / Entra ID si la empresa lo tiene** — recomendado: sin segundo juego de credenciales, y las bajas de personal se propagan solas. Respaldo: credenciales locales en la base.
- Encima, **PIN de 4 a 6 dígitos** para reentrar durante el turno.
- El teléfono se trata como **compartido por línea**. La línea viaja con el **usuario**, nunca con el dispositivo (§2.3).
- El PIN **nunca** abre la sesión de otro usuario.

> **Por qué el PIN:** teclear una contraseña completa con guantes, de pie y en movimiento (§12.3) es irreal. Sin PIN, el comportamiento inevitable es dejar la sesión abierta indefinidamente, que es peor para la seguridad que el PIN.

*Impacta:* 04, 05, 02, 03.

## D7 · Retención — 🟡 Supuesto declarado, requiere validación legal

- **Operativo** (asignaciones, movimientos, paros, desperdicio, producción): retención **indefinida**. Es la materia prima que §12.7 pide explícitamente para calibrar parámetros.
- **Médico**: mientras esté activo, más un periodo configurable tras el cierre — **propuesta: 5 años** —, y después **anonimizado, no borrado**, para que la traza de auditoría sobreviva.

> ⚠ Es una propuesta técnica razonable, **no una afirmación sobre el marco legal aplicable**. Requiere validación con el responsable legal o de salud ocupacional del cliente.

*Impacta:* 04, 05.

---

# E · Entorno — abiertas

Estas no bloquean la redacción, pero sí condicionan decisiones concretas del TRD. Cada una aparece en los documentos afectados como `⚠ PENDIENTE-<ID>` con el supuesto bajo el que se procede.

| ID | Estado | Supuesto de trabajo | Qué cambia si la respuesta es otra |
|---|---|---|---|
| **E1** Gafete | 🔴 Abierta | Código de barras Code 128 o QR que codifica el número de ficha, leído con cámara y decodificación **en el dispositivo** (sin salida de datos). Búsqueda manual siempre disponible (§12.2). | Si el gafete **no trae código**, hay que leer el número impreso por OCR: mucho menos fiable con guantes y mica rayada. La recomendación honesta pasaría a ser **reimprimir los gafetes con código**. |
| **E2** MDM | 🔴 Abierta — **ahora bloqueante** | Existe MDM / Device Owner en los dispositivos de planta. | Sin MDM, D5 no se puede garantizar y hay que decidir entre aceptar FCM como campana vacía o aceptar el hueco. **Es la pendiente de mayor impacto.** |
| **E3** Servidor | 🔴 Abierta | Windows Server on-premise + SQL Server 2019 o superior. | Si permiten Linux o contenedores, la elección de stack no cambia; solo cambia el empaquetado. |
| **E4** Dispositivos | 🔴 Abierta | `minSdk 26` (Android 8.0) como piso conservador, `targetSdk` actual. | Un parque más moderno permite subir el piso y simplificar; uno más antiguo obliga a revisar el servicio en primer plano. |
| **E5** Red | 🔴 Abierta | Wi-Fi interno de planta, sin salida a internet garantizada. | Es justo lo que sostiene la arquitectura de D5 sin terceros. |
| **E6** Dossier PDF | 🔴 Abierta | `SmartAssign_Dossier_Arquitectura_Normas_APA7.pdf` **no** se ha leído y **no** se considera fuente de verdad. | Si lo es, hay que leerlo y revisar todo lo anterior contra él antes de construir. |
| **E7** Línea base de KPIs | 🔴 Abierta | No existe medición previa (§1.1: "todo se estima al final del día"). Los KPIs del PRD se proponen con línea base *a establecer en las dos primeras semanas*. | Si hay cifras actuales, los objetivos del PRD dejan de ser propuestas y pasan a ser metas verificables desde el día uno. |
| **A5b** Donante L4 | 🔴 Abierta | L4 queda excluida de la extracción inversa **por ser la solicitante** en el ejemplo, no de forma permanente. | Si nunca puede ser donante, la derivación de A5 necesita una exclusión fija adicional. |
| **Git** | 🔴 Abierta | El proyecto **no es un repositorio git**. No se ha inicializado ninguno. | Sin control de versiones, estos documentos no tienen historial y las decisiones no son rastreables en el tiempo. Se recomienda `git init`. |

---

## Resumen de estado

| Bloque | Cerradas | Supuestos | Abiertas |
|---|---|---|---|
| A · Correcciones a la fuente | 10 | 1 | 1 (A5b) |
| B · Motores | 12 | — | — |
| C · Flujos y estados | 15 | — | — |
| D · Seguridad | 6 | 1 | — |
| E · Entorno | — | — | 8 |
| **Total** | **43** | **2** | **9** |
