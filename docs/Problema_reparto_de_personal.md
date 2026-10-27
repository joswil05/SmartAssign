# Reparto de personal en una planta de envasado: el problema

## 1. En pocas palabras

Una planta industrial de envasado reparte cada turno unas 160 personas entre 10 líneas de producción. Hoy ese reparto se decide de palabra y se anota en papel. El coordinador y los supervisores lo resuelven de memoria mientras caminan por la planta, y eso produce cinco pérdidas:

1. Nadie sabe con certeza dónde está cada persona.
2. Se pone a gente en puestos que su condición médica no permite.
3. Nadie controla el desgaste físico.
4. Cuando una línea se para, se pierde tiempo productivo.
5. No queda registro de lo que pasó en el turno.

Lo que sigue explica cómo está organizada la planta, cómo transcurre un turno y dónde se rompe el proceso actual.

## 2. Las líneas

La planta tiene 10 líneas, de L1 a L10. Hoy cinco operan con personal real: L1, L2, L4, L6 y L8. Las otras cinco están previstas, pero todavía no tienen personal ni productos asignados.

En un día dado, cada línea puede estar:

| Situación | Qué significa |
|---|---|
| Activa | Tiene un producto planificado y opera hoy |
| Inactiva | No tiene producto planificado y sus puestos no existen hoy |
| En arranque | Se está llenando de gente al inicio del turno |
| En producción | Funciona con normalidad |
| En paro | Detenida por una avería u otra contingencia |
| En limpieza | Cambio de producto entre lotes |

### Prioridad entre líneas

No todas las líneas valen lo mismo. Cuando la gente no alcanza, este es el orden de importancia:

**L4 > L1 > L2 > L6 > L7 > L5 > L3 > L8 > L9 > L10**

El orden no es fijo. Lo decide el coordinador y cambia por razones de negocio: un pedido urgente de un cliente importante, una avería, una alerta de calidad que obliga a reforzar una línea. Puede cambiar de un día para otro y también en medio del turno.

### Distancia entre líneas

Caminar de una línea a otra lleva tiempo, y cada línea está a distinta distancia de las demás. El cliente entregó, para cada línea, el orden de las otras de la más cercana a la más lejana:

| Desde | De más cercana a más lejana |
|---|---|
| L1 | L2, L4, L9, L10, L6, L3, L7, L5, L8 |
| L2 | L4, L1, L7, L9, L10, L3, L6, L5, L8 |
| L3 | L10, L9, L6, L7, L4, L2, L1, L5, L8 |
| L4 | L2, L1, L7, L9, L10, L6, L3, L5, L8 |
| L5 | L1, L2, L4, L7, L9, L10, L6, L3, L8 |
| L6 | L3, L10, L9, L7, L4, L2, L1, L5, L8 |
| L7 | L9, L10, L6, L3, L4, L2, L1, L5, L8 |
| L9 | L3, L10, L6, L7, L4, L2, L1, L5, L8 |
| L10 | L9, L3, L6, L7, L4, L2, L1, L5, L8 |

La tabla no es simétrica, y eso es intencional: refleja los recorridos reales por los pasillos, no la distancia en línea recta. Por ejemplo, desde L5 la más cercana es L1, pero desde L1 la L5 es de las últimas.

Importancia y cercanía son dos datos distintos. Uno dice qué línea importa más; el otro, cuánto hay que caminar.

### La L8

La L8 no tiene máquinas ni puestos técnicos. Son mesas de ensamble manual, donde siempre hay trabajo y nunca es urgente. Tiene su propio supervisor.

## 3. El personal

Hay dos familias, y casi todo depende de esa diferencia: los **operadores** (personal técnico) y los **operarios** (personal general). En esta planta "operador" y "operario" no son sinónimos.

| Categoría | Qué es | Dónde trabaja |
|---|---|---|
| **Operador A** | Máxima calificación técnica. Indispensable | En su máquina, que es un puesto fijo |
| **Operador B** | Técnico calificado y muy versátil | En puestos rotativos mientras el Operador A titular está presente. Cuando falta un A, lo reemplaza en su máquina |
| **Operador C** | Técnico júnior en formación | En un puesto fijo junto a un Operador A, o en puestos rotativos |
| **Averiero** | Técnico de mantenimiento mecánico | En su puesto fijo de mantenimiento |
| **Operario** | Personal general, el grupo más numeroso | En puestos rotativos: tareas físicas y repetitivas |
| **Liderazgo** | Supervisores, jefes, coordinadores, analistas, administrativos | Normalmente no ocupa puestos. En una falta grave puede cubrir uno |

Hoy el padrón tiene 164 personas activas: unos 120 operarios, 26 operadores de equipo, 3 averieros y unas 15 personas de liderazgo y administración.

### El Operador B es el recurso más disputado

Es el único que se mueve entre las dos familias. Si falta un Operador A y no hay un B disponible, la máquina queda sin quien la opere. Por eso todas las líneas quieren tener operadores B, y no hay suficientes.

### Quién puede ocupar cada puesto

| Tipo de puesto | Pueden ocuparlo |
|---|---|
| Puesto fijo de Operador A | Operador A, Operador B |
| Puesto fijo de averiero | Averiero, Operador B |
| Puesto fijo de Operador C | Operador C, Operador B, Operador A |
| Puesto rotativo | Operario, Operador B, Operador C |

Los Operadores A y los averieros no pasan a puestos rotativos, porque su calificación hace falta en su máquina.

## 4. Los puestos

En las líneas con datos hay hoy 98 puestos: 31 fijos y 67 rotativos.

| | Puesto fijo | Puesto rotativo |
|---|---|---|
| Qué es | Técnico, atado a una máquina o función | Tarea general, física y repetitiva |
| Quién lo ocupa | Operadores | Operarios, y si hace falta Operadores B o C |
| Titular | Tiene un titular, que es quien suele ocuparlo | No tiene un dueño fijo |
| Durante el turno | La misma persona se queda todo el turno | La gente tiene que ir rotando por el desgaste |
| Desgaste | No se toma en cuenta | Es la razón por la que hay que rotar |
| Durante un paro | Sigue ocupado: los técnicos son quienes reparan | Se queda sin tarea, y su gente queda parada |

### Tiempo en el puesto y tiempo de recuperación

Para cada puesto rotativo, la planta ya tiene registrados dos tiempos:

- **Tiempo en el puesto:** cuántas horas puede estar alguien antes de necesitar relevo. Según el puesto, va de 1 a 8 horas.
- **Tiempo de recuperación:** cuántas horas tienen que pasar antes de que esa persona vuelva al mismo puesto. Va de 1 a 48 horas.

| Puesto (ejemplos reales) | Tiempo en el puesto | Recuperación |
|---|---|---|
| Revisión 1,1 | 1 h | 3 h |
| Revisión 2 | 1 h | 2 h |
| Girar botellas 1, 2 y 3 | 2 h | 24 h |

Estos tiempos están en una planilla. En el piso de la planta nadie los controla mientras se trabaja.

### Otros datos de cada puesto

- **Exigencias físicas.** Cada puesto exige ciertos esfuerzos. Enfermería dicta qué esfuerzos tiene prohibidos cada persona. No registra diagnósticos, solo lo que la persona no puede hacer.
- **Sexo preferente.** Casi todos los puestos tienen una preferencia por razones ergonómicas: masculino, femenino o indistinto. Es una preferencia, no una prohibición, y si hace falta se puede pasar por alto.
- **Puestos que hoy no existen.** Los puestos que se usan dependen del producto que hace la línea. Un puesto que el producto de hoy no necesita no está ni libre ni ocupado: hoy no existe. Si se lo toma por libre, alguien intenta llenar un puesto que no hace falta. Si se lo toma por ocupado, la línea parece llena cuando no lo está.

## 5. Dónde puede estar cada persona

En cada momento, una persona está en una sola de estas situaciones:

- Todavía no llegó, o su turno no empezó.
- Llegó y espera en la sala de espera, sin puesto.
- Está trabajando en un puesto.
- Está caminando de un punto a otro de la planta.
- Está en la L8 haciendo ensamble manual.
- Está retirada temporalmente, por ejemplo porque fue a enfermería.
- Está ausente con justificación: vacaciones, permiso, cita médica, subsidio o accidente laboral.

Además, cada persona tiene una línea habitual, que es donde suele trabajar.

## 6. Los dos roles

**Coordinador.** Hay uno por planta y ve las 10 líneas. El día anterior decide qué líneas operan, qué producto hace cada una y qué supervisor va a cada línea. Arranca el turno, puede intervenir en cualquier línea y mantiene el padrón del personal. A veces mueve a alguien por un acuerdo directo con el trabajador, como un permiso o un arreglo de palabra.

**Supervisor.** Hay uno por cada línea activa, y cada supervisor tiene una sola línea. Responde solo por la suya: llena los puestos rotativos, vigila el desgaste, pide relevos y recibe gente, registra paros y desperdicio, y cierra el turno.

## 7. Cómo transcurre un turno

**El día anterior.** El coordinador decide qué líneas operan y con qué producto, asigna los supervisores y revisa si la gente alcanza, teniendo en cuenta las ausencias que ya conoce.

**El arranque.** La gente marca entrada en el reloj checador y espera en la sala de espera. Los operadores van a sus máquinas. Si falta el titular de una máquina, lo cubre un Operador B, y si no hay ninguno, la máquina queda sin cubrir. Los puestos rotativos empiezan vacíos: cada supervisor va a la sala de espera, junta a su grupo, lo lleva a la línea y le da un puesto a cada uno. Es el momento de más movimiento del día, con decenas de personas caminando a la vez.

**Durante el turno.** Desde el arranque todos los puestos rotativos están ocupados, así que no hay puestos libres adonde mandar a nadie. Con las horas la gente se desgasta y necesita relevo. Si se saca a alguien antes de que llegue su reemplazo, el puesto queda vacío y la producción se frena. Intercambiar a dos personas que ya están trabajando obliga a dejar un puesto vacío por un momento. Y si el reemplazo se saca de otra línea, se abre un hueco en esa línea.

**Imprevistos.**

- **Paro:** la línea se detiene por un problema mecánico, eléctrico, de calidad o por falta de material. Los técnicos se quedan a reparar y los operarios de esa línea se quedan sin tarea.
- **Cambio de producto:** cambian los puestos que necesita la línea. Aparecen puestos nuevos, otros dejan de existir, y la gente que estaba en estos últimos queda libre.
- **Un titular se va a mitad de turno**, por ejemplo a enfermería: su máquina queda sin operador. Lo cubre un Operador B, esté donde esté, y al sacarlo de su puesto queda otro puesto vacío.
- **Un titular vuelve**, porque regresó de enfermería o llegó tarde: hay que devolverle su máquina y saber a quién se puso en su lugar.
- **Doble turno:** algunas personas trabajan dos turnos seguidos y llegan al segundo ya cansadas.

**Cierre de lote y de turno.** Al terminar cada lote se registra lo producido y el material desperdiciado, separado en dos causas que tienen responsables distintos:

- **Daño de origen:** el material llegó defectuoso por fallas de fábrica, almacén o transporte. Es un problema del proveedor o de logística.
- **Daño de proceso:** la máquina destruyó material, casi siempre por estar descalibrada. Es un problema de mantenimiento.

La planta mide la eficiencia así:

> Eficiencia = producción real ÷ (tiempo efectivo de marcha × ritmo teórico del producto)

El tiempo efectivo de marcha es el tiempo de turno transcurrido menos la suma de los paros. El ritmo teórico depende de cada producto. Hoy todo esto se estima a ojo al final del día.

## 8. Dónde falla hoy

1. **Gente que se pierde en el trayecto.** Mientras alguien va de una línea a otra, nadie lo tiene bajo control. Si no llega, nadie lo nota hasta que falta.
2. **Dos supervisores cuentan con la misma persona.** O un supervisor se lleva a alguien que otro estaba esperando, y ninguno se entera hasta que le falta gente.
3. **Efecto dominó.** Para cubrir un hueco en L4 se saca a alguien de L2. Eso abre un hueco en L2, que se cubre desde L6, y así sigue. Hay gente cruzándose por los pasillos y supervisores que no entienden por qué se quedaron sin personal.
4. **Las restricciones médicas no llegan a quien decide.** Enfermería las dicta, pero están en una carpeta. El supervisor decide de pie, en la planta, sin esa información.
5. **Gafetes cambiados.** Los gafetes se prestan, se confunden y se intercambian. Si se identifica mal a alguien, puede terminar en un puesto que su condición le prohíbe.
6. **Desgaste que nadie ve.** Nada avisa cuando alguien lleva demasiado tiempo en un puesto, ni cuando vuelve a una tarea desgastante antes de recuperarse (por ejemplo, girar botellas dos días seguidos). Quien hace doble turno parece igual de descansado que quien recién empieza.
7. **Gente parada durante un paro.** Cuando una línea se detiene, sus operarios quedan sin hacer nada, porque nadie ve la planta entera para mandarlos adonde hacen falta.
8. **Arranque desordenado.** Decenas de personas caminan a la vez y todas las líneas compiten por la misma gente. Es fácil que alguien termine en una línea distinta de la que lo esperaba.
9. **Titulares que vuelven.** Cuando un titular regresa, nadie recuerda con certeza qué máquina era la suya ni a quién se puso en su lugar.
10. **El turno no deja registro.** Cuánto se paró y por qué, cuánto se desperdició y por qué causa, qué tan eficiente fue cada línea, cuánto tarda la gente en moverse: todo se reconstruye de memoria al final del día, y a veces ni eso.

## 9. Condiciones del entorno

- Nadie trabaja en un escritorio. El coordinador y los supervisores están de pie, caminando por la planta, muchas veces con una sola mano libre.
- La iluminación es industrial y cambia de una zona a otra.
- La red de la planta es inestable y tiene zonas sin señal.
- La información médica es sensible y no debería llegar a más gente de la necesaria. Cada supervisor responde solo por su línea.
- Hay un reloj checador para la asistencia.
- Cada persona lleva un gafete con un número impreso.

## 10. Lo que todavía falta o no se sabe

- El padrón no dice quiénes son Operadores B ni C, así que hoy no se sabe quiénes son las piezas más disputadas.
- Casi no está documentado qué puestos necesita cada producto.
- Las restricciones médicas reales todavía no están en ningún registro, solo en la carpeta de Enfermería.
- Nadie midió cuánto se tarda en caminar de una línea a otra.
- L3, L5, L7, L9 y L10 todavía no tienen personal ni productos definidos.
