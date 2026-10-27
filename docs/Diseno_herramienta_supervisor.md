# SmartAssign: diseño de la herramienta del supervisor

Borrador vivo, actualizado el 23 de octubre de 2026. Reemplaza el alcance de `Propuesta_solucion_SmartAssign.md`. Los valores numéricos son iniciales y están reunidos en la sección 16 para que la planta los valide.

## 1. Alcance

**Entra:**
- Una app para el supervisor de cada línea.
- Asignación de puestos fijos: operadores A, con cobertura de un B cuando falta el titular. Los puestos de C los asigna el supervisor a su criterio. Los averieros se tratan como un puesto fijo más que un B puede cubrir.
- Asignación de puestos varios: operarios, y B o C si hace falta.
- Relevos en tiempo real para los puestos varios, incluidas las comidas.
- Movimientos de personas entre líneas.
- Una sección de Configuración dentro de la app para dar de alta al personal y mantener los datos (sección 15).

**No entra por ahora:** planificación del día anterior, funciones del coordinador, registro de lotes y desperdicio, indicadores de eficiencia.

**Condiciones:** solo teléfonos Android y costo cero, tanto en la app como en su desarrollo (sección 17).

## 2. Conceptos base

- **Dueño único.** Cada persona pertenece a una sola línea a la vez, o está en la sala o "en camino". Solo el supervisor de esa línea puede asignarla. Al llegar, quien tiene línea habitual pertenece a esa línea.
- **Escanear es recibir.** Una persona entra a tu línea cuando escaneas su gafete. Solo puedes escanear a alguien de tu línea, a alguien libre en la sala o a alguien que viene en camino hacia ti. Si escaneas a alguien de otra línea, la app te ofrece pedírselo a su supervisor.
- **L8, la reserva de la planta.** En la planta no hay gente libre esperando. Desde el arranque, cada persona ocupa un puesto o está en L8, en las mesas de ensamble, donde siempre hay trabajo y nada es urgente. Por eso:
  - **el relevista sale de L8 y el relevado vuelve a L8,** donde sigue produciendo mientras se recupera y queda disponible para relevar otro puesto;
  - **todo hueco se cubre primero desde L8,** sea un relevo, una ausencia o alguien que se retira;
  - **cada relevo se pide con la anticipación que exige la caminata,** porque según la tabla de cercanía L8 es la línea más lejana desde todas las demás. La app usa esa tabla hasta tener los tiempos reales, que mide en cada traslado confirmado.
- **Tipo de puesto.** Agrupa puestos equivalentes de distintas líneas, por ejemplo girar botellas en L2 y en L4. La recuperación se cuenta por tipo, así acompaña a la persona cuando cambia de línea y entre un día y el siguiente. Si dos puestos del mismo tipo tienen distinta recuperación en distintas líneas, se aplica la del puesto que la persona dejó.
- **Reloj del puesto.** Minutos efectivos que una persona lleva en su puesto varios actual. Corre solo mientras la línea produce y la persona está en el puesto.

| Elemento | Estados |
|---|---|
| Persona | En sala · en puesto · en L8 · en camino · fuera (motivo: comida, salida corta, enfermería, permiso, accidente, se retiró) · ausente · fin de turno |
| Puesto | Con titular · cubierto por B · ocupado · vacante · en espera (salida corta) · no se usa hoy |
| Línea | Produciendo · en paro · en limpieza (cambio de producto) · en comida (solo si toda la línea para a comer) |

Cada persona tiene además dos marcas por turno: **doble turno** y **ofrecida** (cuando su línea la presta durante un paro).

## 3. Datos

- **Personas:**
  - nombre, número de gafete, código QR, categoría (A, B, C, averiero, operario, liderazgo), turno habitual, línea habitual, sexo, fecha de ingreso;
  - si es A o averiero, la máquina o el puesto del que es titular;
  - estado: activa, pendiente de validar (alta rápida) o dada de baja. Nunca se borra a nadie, porque el historial sostiene la recuperación y los reportes;
  - foto, si dio su consentimiento;
  - tipos de puesto para los que todavía no está habilitada;
  - restricciones médicas, cuando se activen.
- **Puestos:**
  - línea, si es fijo o varios, tipo de puesto;
  - titular, en los fijos;
  - tiempo máximo y recuperación, en los varios;
  - carga (ligera, media o pesada), sexo preferente;
  - si el puesto puede quedar unos minutos sin nadie (si no se indica, se asume que sí).
- **Líneas:** supervisor, cercanía con las demás líneas, orden de prioridad y mínimo de gente que no se le puede quitar (sección 11). La L8 no tiene puestos varios ni relojes: su gente trabaja en las mesas de ensamble y es la reserva de la planta.
- **Plantillas de producto:** qué puestos usa cada producto. La app las aprende de lo que marca el supervisor y las propone la vez siguiente.
- **Turnos:** horario de cada turno y hora de comida.
- **Historial:** todas las asignaciones con su inicio y su fin. De ahí salen la recuperación, el doble turno y los reportes.

**Fuente de los datos: `Base de Datos.xlsx`** (en la carpeta del proyecto anterior).

| Hoja | Qué tiene | Qué se importa |
|---|---|---|
| Personal | 164 personas: código de empleado, nombre, sexo, perfil, línea asignada, fecha de ingreso, activo | Todo lo anterior. **No se importan cédula, INSS ni fecha de nacimiento:** la app no los necesita |
| Puestos Fijos | Los 98 puestos de L1 a L7: 67 con tiempo y recuperación (puestos varios) y 31 sin tiempo (máquinas y puestos técnicos) | Todo |
| Puestos SKU | Puestos que solo existen con ciertos productos, como las lámparas de L1 o los stickers de L4 | Como plantillas de producto |
| Personal ausente | Ausencias con tipo (vacaciones, permiso, subsidio, consulta, emergencia) y horario | Como ausencias conocidas |
| Programa | Producción planificada por línea y turno | No en la versión 1 |

**Lo que hay que corregir o completar antes de importar:**
- **Categorías A, B y C:** el perfil solo dice "OPERADOR DE EQUIPOS" (22 personas), sin distinguir A, B ni C. Sigue siendo el dato indispensable que falta.
- **Columna "SexoPreferente":** tiene valores que no son de sexo ("Operador", "Estibador", "Supervisor", "Averiero") y una variante ("Femenina").
- **Puestos dudosos:** hay puestos llamados "Supervisor" y "Averiero" que tienen tiempo de rotación, como si fueran puestos varios (L05001, L05005).
- **Perfiles especiales:** "Estibador", "Genérico" y "Operador de filtro" se tratan como habilitaciones (sección 15).
- **Líneas asignadas:** mezclan nombres ("LINEA 1", "Linea 8") con códigos (1605, 1606) y áreas como "MAQUILA" y "PET".
- **Tipos de puesto:** hay que agrupar los nombres equivalentes entre líneas, como "Revisión 1" de L2 y "Revisión 1,1" de L1.
- **El script de carga no importa a ciegas:** revisa cada fila y genera un reporte con lo que hay que corregir en el Excel antes de cargar.

## 4. Arranque del turno: asignación por escaneo

La asignación inicial se hace escaneando el código QR del gafete de cada trabajador disponible. Con un solo escaneo la persona queda presente y ubicada en un puesto.

**Antes de escanear:**
- **Puestos activos.** El supervisor elige el producto y la app activa los puestos que ese producto usa, según la hoja "Puestos SKU" de la planilla. Los demás pasan a "no se usa hoy". Se ajusta con un toque.

**Escaneo continuo:**
1. El supervisor toca "Arrancar con escaneo". La cámara queda abierta para escanear un gafete tras otro, sin cerrarla entre persona y persona.
2. **Cada escaneo** identifica a la persona por su código de empleado y muestra:
   - su foto en grande, su nombre y su categoría;
   - la marca de doble turno, si tuvo una asignación en el turno inmediatamente anterior (el supervisor también puede marcarla a mano);
   - el puesto que la app le propone.
3. **Puesto propuesto.** La app recorre estas opciones en orden y propone la primera que se cumpla:
   1. su máquina, si es titular de un puesto fijo de esta línea ("Asignado por asistencia");
   2. si es B y el titular de una máquina ya está registrado como ausente, esa máquina ("Cubre a [titular], ausente");
   3. un puesto varios compatible, sin recuperación pendiente (incluida la del día anterior) y que respete el sexo preferente. Entre los compatibles, prefiere el puesto que hace más tiempo no hace y alterna cargas pesadas y ligeras;
   4. lo mismo, pero sin respetar el sexo preferente, y con aviso;
   5. si no hay ninguno, L8. La app explica qué lo impidió, por ejemplo: "No puede girar botellas hasta mañana a las 10:00 y los demás puestos están ocupados".
4. **Confirmar.** El supervisor mira la foto y toca "Asignar", "Otro puesto" o "A L8". El escaneo por sí solo nunca asigna: la foto es la verificación de identidad. Cada persona lleva 3 o 4 segundos, así que un grupo de 15 queda listo en alrededor de un minuto.
5. **Al revés:** también se puede tocar primero un puesto vacío y escanear después a quien va ahí. Sirve para ubicar a alguien a propósito, y es la única forma de asignar al personal de liderazgo.
6. **Titulares:** un titular A también se puede confirmar con un toque en su máquina ("Llegó Marco"), sin escanear, porque ya se sabe quién es y cuál es su puesto.
7. **Quien llega tarde** se escanea igual, y la app le propone puesto con el mismo orden.

**Ventana de arranque** (los primeros 10 minutos, parámetro):
- El supervisor solo puede escanear a gente de su línea habitual. Así cada uno ordena primero a quien tiene enfrente y nadie se lleva gente de otra línea en el momento de más movimiento.
- Su propia gente se puede escanear sin red, porque ya pertenece a esa línea y no puede haber conflicto.
- Pasada la ventana, la gente que quedó libre en la sala la puede tomar cualquier línea. Gana el primero que escanea, y eso necesita red porque lo confirma el servidor.
- Quien pertenece a una línea que hoy no opera queda libre: va a L8, y pasada la ventana lo puede tomar cualquier línea.

**Al cerrar la ventana:**
- Las máquinas cuyo titular no se escaneó quedan vacantes y la app propone un B (sección 5).
- Los puestos varios que quedaron vacíos se cubren desde L8.
- **Simulación rápida:** con los puestos activos, la gente presente y la comida, la app calcula cuántos relevos va a pedir a L8 en el turno y en qué horas se juntan. Por ejemplo: "entre las 8:00 y las 9:00 vas a necesitar 4 relevos de L8".
- **Escalonado inicial:** el primer ciclo de relevos se escalona (sección 7, paso 4). El reloj de cada persona arranca cuando se confirma su asignación.
- **Comida:** la hora es fija, así que no hay nada que programar. La app ya sabe que a las 12:00 la línea para (sección 8).

## 5. Puestos fijos

1. **Falta el titular** (al arrancar o porque se retira): la máquina queda vacante.
   - Si en la línea hay un B que no esté cubriendo otra máquina, pasa a la máquina, y su puesto varios se cubre desde L8.
   - Si no hay, se pide a otra línea (sección 11).
   - Si no hay B en la línea y no se consigue en otra, la máquina queda como **vacante crítica**: aparece arriba de todo, en rojo, con el texto "Sin titular ni B disponible".
2. **El B deja un puesto varios.** Si el B estaba en un puesto varios, su reloj se detiene y ese puesto entra a la cola como relevo urgente.
3. **Vuelve el titular.** Se toca "Volvió" o se escanea su gafete. El B vuelve a un puesto varios vacante de su línea, o a L8 si no hay ninguno. Si era prestado, se devuelve.
4. **Comida de los titulares.** Con la comida a hora fija la línea entera para, máquinas incluidas, así que no hace falta cubrir a ningún A. Si alguna vez una línea come de forma escalonada, la app programa la comida de cada A y propone un B para cubrir su máquina. Eso puede encadenar un relevo en el puesto varios que deja el B.

## 6. Reloj del puesto

| Situación | Qué hace el reloj |
|---|---|
| Línea produciendo, persona en el puesto | Corre |
| Paro o limpieza | Se pausa. Si la pausa supera el umbral de paro largo, al reanudar vuelve a cero, porque cuenta como descanso |
| Salida corta | Se pausa mientras la persona está fuera |
| Salida larga o relevo | La asignación se cierra y se registra como terminada a esa hora |
| Comida a la hora fija | La línea para y el reloj se pausa. La asignación sigue abierta, así que cada uno vuelve a su mismo puesto. Si la comida dura 30 minutos o más, el reloj vuelve a cero al regresar, igual que después de un paro largo |

- **Límite del reloj:** tiempo máximo del puesto. En doble turno, ese tiempo se multiplica por el factor de doble turno.
- **La recuperación se mide en tiempo de reloj** desde que la persona dejó un tipo de puesto, sin importar paros ni comidas. Sigue contando entre turnos y entre días.

## 7. Motor de relevos

Corre en el teléfono para la propia línea, así funciona sin red. Se recalcula cada minuto y ante cada evento.

1. **Vencimientos.** Para cada asignación abierta: vencimiento = ahora + (límite − minutos acumulados). Si la línea está en pausa, no hay vencimientos hasta que se reanuda.
2. **Comida.** Entre las 12:00 y el regreso no hay vencimientos, porque la línea está parada.
   - Si un puesto vence en los últimos minutos antes de la comida, el relevo se hace igual, salvo que Enfermería autorice una tolerancia (parámetro, inicialmente 0).
   - Al regresar, todos los relojes arrancan juntos desde cero, así que la app vuelve a escalonar el primer ciclo, como en el arranque.
3. **Horizonte.** Se toman los vencimientos de los próximos 60 minutos.
4. **Escalonado.**
   - Cada relevo recibe una hora programada que nunca es posterior a su vencimiento.
   - Dos relevos de la misma línea quedan separados al menos por la separación mínima, para que el supervisor alcance a hacerlos.
   - Se programan **lo más tarde posible**. Se recorren del vencimiento más tardío al más temprano, y cada uno toma el último hueco libre antes de su vencimiento. Así nadie sale antes de lo necesario.
5. **Elección del relevista**, en orden de hora programada.
   - **Dónde busca, en este orden:**
     1. **L8.** Es siempre la primera opción. Incluye a quienes volvieron a L8 después de relevos anteriores, cuando cumplen el tiempo mínimo en L8. **La línea decide cuándo y para qué puesto, y L8 decide quién.** La línea pide, por ejemplo, "un relevista para Revisión 2, aquí a las 9:52". La app de L8 elige a la persona con los mismos filtros y preferencias, porque esa gente le pertenece, y la manda con la anticipación de la caminata.
     2. **Intercambio dentro de la línea,** solo si en L8 no hay nadie que sirva: dos personas cuyos relevos vencen cerca se cambian de puesto, si las reglas lo permiten en los dos sentidos. Nadie camina, pero cada puesto queda unos segundos vacío, así que solo se usa entre puestos que pueden esperar.
     3. **Gente de una línea en paro o ya ofrecida** (versión 2).
     4. **Último recurso:** alguien de la línea activa de menor prioridad (sección 11).
   - **Filtros, que eliminan candidatos:**
     - categoría que no corresponde al tipo de puesto;
     - en recuperación de ese tipo de puesto;
     - no habilitado para ese tipo;
     - restricción médica que lo impide;
     - doble turno y puesto pesado;
     - en comida escalonada, comida programada dentro de los próximos 20 minutos.
   - **Preferencias, que suman o restan puntos:**
     - suma: más tiempo en L8;
     - suma: más tiempo desde la última vez que hizo ese tipo de puesto;
     - resta: menos minutos de puestos pesados acumulados en el turno;
     - resta: puesto pesado justo después de otro pesado;
     - resta: no cumplir el sexo preferente;
     - resta: no ser de la línea habitual.
   - Se elige al candidato con más puntos. Las dos mejores alternativas se guardan para que el supervisor pueda cambiar.
   - En la primera versión las preferencias se aplican en el orden de la lista y no con pesos. Los pesos se ajustan en el piloto, a partir de las sugerencias que los supervisores cambien.
   - **Comida adelantada** (solo en comida escalonada): si a quien sale le toca comer dentro de los próximos 15 minutos, sale directo a comer y se ahorra un movimiento.
   - Quien sale vuelve a L8, y desde que llega es candidato para otros relevos, en su línea o en otra.
6. **Reserva de L8.**
   - Con los relevos programados, la app calcula cuánta gente tiene que tener L8 disponible en cada momento.
   - **Si L8 se va a quedar corta,** avisa con anticipación al supervisor de L8 y a la línea afectada, y busca las otras opciones del paso 5. Por ejemplo: "a las 11:00 L8 no tiene a nadie que pueda ir a Revisión 2".
7. **Avisos.**
   - Cada relevo genera dos avisos. El supervisor de L8 lo recibe con la anticipación de la caminata más un margen, para mandar al relevista. El supervisor de la línea lo recibe unos minutos antes de que llegue, para recibirlo.
   - Dos relevos con menos de 2 minutos de diferencia van en un solo aviso.
   - Los avisos se programan en el propio teléfono, así suenan aunque no haya red.
8. **Cola.** El resultado es la cola de "Próximos relevos" que ve el supervisor.

## 8. Comidas

**Modo inicial: hora fija a las 12:00, con la línea parada** (por confirmar con la planta).

- **A las 12:00** la línea pasa sola a "en comida". La app avisa unos minutos antes para que el supervisor sepa que no hay relevos pendientes después de esa hora.
- **Durante la comida:**
  - los relojes están en pausa y las máquinas, paradas;
  - no hace falta cubrir a nadie.
- **Al regresar:**
  - el supervisor toca "Reanudar" y la línea vuelve a producir; si se le olvida, la app lo hace sola cuando termina la duración configurada;
  - cada persona vuelve a su mismo puesto, sin moverse ni buscar a dónde ir;
  - como la comida cuenta como descanso, los relojes arrancan desde cero;
  - la app escalona el primer ciclo de la tarde, igual que en el arranque, para que no venzan todos juntos.
- **Prestados:** quien está prestado come con la línea donde está y vuelve a ella después.
- **Ausentes al regreso:** quien no vuelve se marca con "Sale" y su puesto entra a la cola como relevo urgente.

**Modo alternativo: comida escalonada**, para una línea que no pueda parar.
- La app reparte la hora de comida de cada persona dentro de una ventana, respetando cuántas pueden comer a la vez.
- Cada salida a comer es un relevo: alguien de L8 entra al puesto y, al volver, quien comió va a L8.
- Si la ventana no alcanza, la app lo dice al arrancar.

## 9. Ejecución en planta

- El supervisor toca el relevo siguiente, escanea al relevista, ve su foto y confirma. Le lleva unos 5 segundos. Quien estaba en el puesto sale "en camino" a L8, y el supervisor de L8 confirma su llegada escaneándolo.
- Escanear a una persona distinta de la sugerida es válido si pasa los filtros. Si no, la app explica por qué no puede: "Carlos puede girar botellas desde las 11:30".
- **Posponer 10 minutos** está permitido mientras no se pase del vencimiento.
- **Relevo vencido:** la tarjeta se pone roja, el teléfono vibra cada 5 minutos y el relevo queda registrado como tardío.
- **Confirmar sin escaneo** es posible, pero queda marcado para revisión.
- **Rechazar una sugerencia:** la app no vuelve a proponer a esa persona para ese puesto en lo que queda del turno. El supervisor puede borrar esa lista de rechazos.
- **Intercambiar:** dos personas de la línea cambian de puesto en una sola operación. La app revisa las reglas en los dos sentidos.
- **Pedir relevo ahora:** el supervisor puede adelantar el relevo de cualquier puesto, por ejemplo si la persona se siente mal, va lenta o hay un conflicto. La persona sigue en su puesto hasta que llega quien la releva: el puesto nunca se libera antes.
- **Toda negativa explica la causa y qué hacer**, en lenguaje de planta. Nunca un error genérico.
- **Sin doble toque:** cada acción se bloquea mientras se procesa. La app distingue "cargando" de "vacío", para que nadie reasigne a gente que ya estaba colocada.
- **Plan B:** una tablet en la línea donde los operarios escanean su gafete, solo si una línea tiene más relevos de los que el supervisor puede atender.

**Cada puesto dice por qué está como está:**

| Situación | Texto en la tarjeta |
|---|---|
| Titular presente | "Asignado por asistencia" |
| B cubriendo una máquina | "Cubre a Luis P., titular ausente" |
| Máquina sin nadie | "Sin titular ni B disponible" |
| Puesto que no se usa hoy | "No se usa con este producto" |
| Reloj en amarillo | "Relevo en 10 min" |
| Reloj vencido | "Pasó su tiempo hace 5 min" |
| Línea en paro | "En pausa por paro" |
| Salida corta | "Salió hace 4 min" |
| Vuelve el titular | "Titular de vuelta, B liberado" |
| Cambio sin enviar | "Pendiente de enviar, sin red" |

## 10. Imprevistos

| Evento | Acción del supervisor | Lógica |
|---|---|---|
| Salida corta (baño) | "Sale", motivo corto | Reloj en pausa y puesto "en espera". Si el puesto no puede quedar sin nadie, se pide un relevo a L8 como cualquier otro. Si la salida pasa de 10 min, se convierte en salida larga |
| Salida larga (enfermería, permiso, accidente, se retira) | "Sale" con el motivo | La asignación se cierra y el puesto entra a la cola como relevo urgente. Un accidente queda registrado con la hora y el puesto |
| Vuelve | Escanea a la persona | Vuelve a su puesto si sigue vacante; si no, va a L8. Si es titular A, vuelve a su máquina (sección 5) |
| Paro | "Paro". Elige la categoría (mecánica, eléctrica, calidad, falta de material) y la causa concreta dentro de ella. Indica la duración estimada: 15, 30 o 60 min, o "no sé". La nota es opcional | Los puestos fijos siguen ocupados, porque los técnicos son quienes reparan. Los relojes de los puestos varios se pausan y la cola se congela. Si la duración estimada es de 30 minutos o más, la app propone mandar a la gente de puestos varios a L8 u ofrecerla a otras líneas. Si se marcó "no sé", lo propone cuando el paro cumple 20 minutos |
| Fin del paro | "Reanudar" | Si fue un paro largo, los relojes vuelven a cero y se escalona otra vez. Si hay gente en L8 o prestada, la app propone traerla de vuelta y cuenta su caminata |
| Cambio de producto | "Cambio de producto" y elige el producto | La línea pasa a limpieza y los relojes se pausan. La app propone los puestos del nuevo producto. Al arrancar, la gente de los puestos que desaparecen ocupa los puestos nuevos si las reglas lo permiten; si sobra, va a L8, y si falta, se pide a L8. Después se escalona otra vez |
| Un B pasa a una máquina | Asignación desde la sección 5 | Su puesto varios entra a la cola como urgente |

## 11. Movimientos entre líneas

| Momento | Supervisor que pide | Supervisor que presta | App |
|---|---|---|---|
| Pedir | Pide una persona concreta, o "1 persona para Girar botellas" | | Busca primero en L8. Si ahí no hay nadie que sirva, en líneas en paro o gente ofrecida, y como último recurso en la línea activa de menor prioridad. Solo muestra a quien pasa los filtros para ese puesto |
| Decidir | | Acepta o rechaza; si acepta, puede cambiar a la persona | Le muestra el impacto: "si prestas a Luisa, tu relevo de las 10:05 queda sin nadie". La prioridad entre líneas aparece solo como referencia |
| Traslado | Ve "en camino" con la hora esperada de llegada | La persona sale de su línea y su cola se recalcula | Nadie más la puede tomar. Si no llega a tiempo, avisa a los dos supervisores |
| Llegada | La escanea: desde ese momento es de su línea | Recibe el aviso de llegada | Arranca el reloj del puesto. La recuperación y la marca de doble turno viajan con la persona |
| Regreso | "Devolver" | "Pedir de regreso" | La persona vuelve en su siguiente relevo, para no dejar su puesto vacío |

Estas operaciones necesitan red.

**Casos especiales:**
- Una solicitud sin respuesta vence a los 3 minutos y la app sugiere la siguiente línea.
- L8 acepta sola, porque su trabajo nunca es urgente.
- En un paro, quien pide a gente ofrecida se la lleva sin otra aprobación.
- Pedir un B para una máquina sigue el mismo circuito.
- A la hora de comida, la persona prestada come con la línea donde está y vuelve a ella después.
- **Un solo salto por movimiento.** Quien cambia de línea sale de L8 o vuelve a L8. Hay dos excepciones: una línea en paro que ofrece a su gente, y el último recurso. El hueco que deja una persona tomada como último recurso **nunca se tapa sacando a alguien de otra línea**: se cubre desde L8 en cuanto haya gente. Así no empieza el efecto dominó.
- **Último recurso, cuando L8 no tiene a nadie que sirva:** la app propone tomar a alguien de la línea activa de menor prioridad, recorriendo el orden de prioridad al revés (L10, L9, L3, L5…). No toca a una línea que ya está en su mínimo de gente. Si todas están en el mínimo, lo dice: "No hay gente disponible en la planta". El supervisor de esa línea acepta o rechaza, como en cualquier préstamo.

## 12. Fin y cambio de turno

- A la hora de fin, la app propone "Cerrar turno". Al cerrarlo:
  - se cierran todas las asignaciones con esa hora;
  - terminan los préstamos;
  - todos pasan a "fin de turno";
  - se borran del teléfono las fotos guardadas durante el turno.
- Si el supervisor no lo cierra, el turno se cierra solo 30 minutos después.
- La recuperación no se reinicia. Si alguien giró botellas a última hora, al día siguiente sigue bloqueado hasta que se cumplan las 24 horas.
- Quien se queda a un segundo turno vuelve a escanearse en el turno nuevo, queda marcado como doble turno y no puede seguir en el mismo tipo de puesto si está en recuperación.

## 13. Reglas

- **Duras** (la app no deja hacerlo):
  - la categoría de la persona corresponde al tipo de puesto;
  - se respeta la recuperación por tipo de puesto, entre líneas, turnos y días;
  - la persona está habilitada para ese tipo de puesto;
  - se respetan las restricciones médicas, cuando haya datos;
  - quien hace doble turno no recibe puestos pesados (se puede desactivar);
  - solo el dueño de una persona puede asignarla;
  - un solo salto por movimiento: el hueco que deja una persona tomada de otra línea solo se cubre desde L8;
  - el personal de liderazgo nunca se propone automáticamente: solo se asigna a mano, eligiendo primero el puesto.
- **Blandas** (la app avisa y deja pasar con un toque):
  - sexo preferente;
  - línea habitual;
  - distancia;
  - dos puestos pesados seguidos;
  - en comida escalonada, puesto asignado justo antes de la comida.
- **Escalonada:** el tiempo máximo en el puesto. No se puede bloquear, pero los avisos escalan.

## 14. Operación de la herramienta

- **Acceso:**
  - la primera vez se entra con el usuario que entrega el administrador;
  - después se abre con PIN de 4 dígitos o con huella;
  - pide el PIN de nuevo si la app pasó 30 minutos sin uso.
- **Dos líneas:**
  - un supervisor puede "tomar" una línea que no tiene supervisor activo, o recibirla de otro con "Entregar línea";
  - la cola muestra las dos líneas con una etiqueta y los avisos indican de qué línea son.
- **Batería:**
  - aviso al 20 %;
  - antes de apagarse, la app dice cuántos cambios faltan sincronizar;
  - "Pasar la línea a otro teléfono" retoma el estado desde el servidor.
- **Teléfono perdido:**
  - el administrador cierra la sesión a distancia;
  - el teléfono guarda solo lo del turno en curso;
  - las fotos de la gente de la línea se guardan durante el turno, para verlas sin red, y se borran al cerrarlo.
- **Sin red:**
  - funciona todo lo de la propia línea: relevos, escaneos de su gente, salidas, paros;
  - necesita red lo que involucra a otra línea o a gente de la sala que no es de su línea;
  - cada acción hecha sin red se guarda como un evento con un identificador único y se envía cuando vuelve la señal. El servidor descarta los duplicados, así que reenviar no repite nada;
  - los puestos con cambios sin enviar muestran "Pendiente de enviar, sin red";
  - al recibir los eventos, el servidor vuelve a revisar las reglas duras. No deshace lo que ya pasó en la planta, pero si algo no cumplía una regla lo marca y avisa al supervisor y al administrador.
- **Configuración:** ver la sección 15.
- **Consentimiento:** si una persona no autorizó su foto, al escanearla solo se ven su nombre y su número.

## 15. Configuración

Es una sección dentro de la misma app. En la versión 1 se usa desde el teléfono, donde es práctico tomar la foto y escanear el gafete. Más adelante, la misma app se podrá abrir en el navegador de una computadora (Flutter web), que es más cómodo para cargar muchos datos.

**Quién entra y qué puede hacer:**

| Rol | Puede |
|---|---|
| Administrador | Todo: personal, puestos, líneas, productos, turnos, parámetros y usuarios |
| Supervisor | Ver el personal. Dar de alta rápida a alguien que llegó y no está en el sistema. Marcar que alguien de su línea ya está habilitado para un tipo de puesto. Tomar la foto de quien no tiene, si dio su consentimiento |
| Enfermería | Solo las restricciones médicas. Nadie más las ve |

**Personal: la ficha de cada persona**

| Campo | Obligatorio | Notas |
|---|---|---|
| Nombre completo | Sí | |
| Número de gafete | Sí | Único. La app avisa si ya existe |
| Código QR | Sí | Contiene el código de empleado de la planilla. Se escanea la etiqueta que ya tiene o la app genera una nueva. Lo que frena los gafetes prestados es la foto en la confirmación |
| Categoría | Sí | A, B, C, averiero, operario o liderazgo |
| Turno habitual | Sí | |
| Línea habitual | No | Sin línea, al llegar queda libre en la sala |
| Máquina de la que es titular | Solo A y averiero | Cada máquina tiene un titular por turno. Si ya lo tiene, la app pregunta si lo reemplaza |
| Sexo | Sí | Solo para la preferencia ergonómica de los puestos |
| Foto | No | Se toma con la cámara, y solo si hay consentimiento |
| Consentimiento de foto | Sí | Sí o no, con la fecha |
| Tipos de puesto no habilitados | No | Por defecto puede hacer todos. Se marcan las excepciones |
| Fecha de ingreso | No | |
| Estado | Sí | Activa, pendiente de validar o dada de baja |

**Acciones:**
- **Agregar persona:** formulario de una sola pantalla.
- **Alta rápida:** la hace el supervisor en la sala cuando llega alguien que no está en el sistema.
  - Pide solo nombre, número de gafete y foto.
  - Entra como operario y solo puede ocupar puestos varios; no puede cubrir máquinas.
  - Queda "pendiente de validar" hasta que el administrador completa la ficha.
- **Editar.**
- **Dar de baja y reactivar.** Si la persona está trabajando, la baja se aplica al cerrar el turno.
- **Imprimir etiquetas QR:** genera una hoja en PDF con las etiquetas de las personas que se elijan.
- **Exportar todo a Excel:** descarga personal, puestos e historial. Sirve también como respaldo semanal, porque el plan gratuito del servidor no hace copias de seguridad.
- **Buscar y filtrar:** se busca por nombre o número, o escaneando el gafete. Se filtra por línea, categoría, turno y estado.

**Cuándo aplica cada cambio:**
- **Alta, foto y habilitación:** al instante en todos los teléfonos con red. Los que están sin red lo reciben al reconectar.
- **Categoría:** vale para las asignaciones nuevas, sin sacar a nadie del puesto que ocupa ahora.
- **Línea habitual, turno y titular de máquina:** desde el siguiente arranque.
- **Registro:** cada cambio queda guardado con quién lo hizo y cuándo.

**Otras partes de Configuración** (solo administrador):
- **Puestos y tipos de puesto:** tiempos, recuperación, carga, sexo preferente, si el puesto puede esperar, titular.
- **Líneas:** supervisor, cercanía con las demás líneas, prioridad y mínimo de gente.
- **Productos:** plantillas de puestos. La app propone las que aprende del uso.
- **Turnos:** horarios y comida.
- **Parámetros:** los de la sección 16.
- **Usuarios:** crear supervisores y Enfermería, reiniciar el PIN, cerrar la sesión de un teléfono perdido.

**Carga inicial:** las 164 personas y los puestos se cargan una sola vez desde el Excel con un script. Después, todo se mantiene desde Configuración.

## 16. Parámetros iniciales

| Parámetro | Valor inicial | Lo valida |
|---|---|---|
| Separación mínima entre relevos de una línea | 3 min | Supervisores |
| Horizonte de la cola | 60 min | Diseño |
| Aviso al supervisor de L8 | Caminata desde L8 hasta la línea + 3 min | Supervisores |
| Aviso al supervisor de la línea | 3 min antes de que llegue el relevista | Supervisores |
| Juntar en un aviso relevos con menos de | 2 min de diferencia | Supervisores |
| Repetición del aviso de un relevo vencido | Cada 5 min | Supervisores |
| Posponer un relevo | 10 min, sin pasar del vencimiento | Supervisores |
| Vencimiento de una solicitud entre líneas | 3 min | Supervisores |
| Paro largo que reinicia relojes | 30 min o más | Enfermería y Seguridad e Higiene |
| La limpieza pausa los relojes | Sí | Planta |
| Duración máxima de una salida corta | 10 min | Planta |
| Modo de comida | Hora fija, la línea para | Planta |
| Hora de comida | 12:00 | Planta |
| Duración de la comida | 30 min | Planta |
| La comida reinicia los relojes | Sí | Enfermería y Seguridad e Higiene |
| Tolerancia para pasar el límite justo antes de la comida | 0 min | Enfermería y Seguridad e Higiene |
| Solo en comida escalonada: no asignar puesto si la comida empieza en menos de | 20 min | Planta |
| Solo en comida escalonada: adelantar la comida si el relevo cae a menos de | 15 min de ella | Planta |
| Factor de tiempo en doble turno | 0.75 | Enfermería y Seguridad e Higiene |
| Doble turno sin puestos pesados | Sí | Enfermería y Seguridad e Higiene |
| Tiempo mínimo en L8 antes de volver a relevar | 0 min | Enfermería y Seguridad e Higiene |
| Ventana de arranque (solo gente de la propia línea) | 10 min | Supervisores |
| Mínimo de gente por línea que no se puede tomar como último recurso | 2 personas, como en el proyecto anterior | Planta |
| Paro: proponer mandar a L8 a la gente de puestos varios | Duración estimada de 30 min o más; si es "no sé", a los 20 min | Supervisores |
| Duración del turno | 8 h o 12 h, según el código del turno | Planta |
| Cierre automático del turno | 30 min después del fin | Planta |
| Bloqueo por falta de uso | 30 min | Diseño |
| Aviso de batería baja | 20 % | Diseño |

**Para confirmar con la planta antes del piloto:**
- que la comida es a las 12:00 para todos, cuánto dura, si para toda la línea (máquinas incluidas) y a qué hora comen los otros turnos;
- cómo es la comida en los turnos de 12 horas, que según la planilla existen junto a los de 8 (códigos 1T8, 2T8, 1T12 y 2T12);
- si el desgaste se detiene en un paro y si en la limpieza los operarios trabajan;
- quién será el administrador de los datos;
- si las restricciones médicas se cargan desde el inicio.

## 17. Tecnología

**Condiciones:** solo Android y costo cero. Todo lo que sigue es gratuito.

| Capa | Tecnología | Para qué |
|---|---|---|
| App | Flutter (Dart), solo Android | La app del supervisor y Configuración |
| Núcleo de lógica | Paquete de Dart puro, con pruebas (`package:test`) | Reglas, reloj, motor de relevos y cambios de estado. Lo usan la app y el modo demostración |
| Modo demostración | Una opción de la propia app, con reloj acelerado y datos de ejemplo | Simular un turno para validar con los supervisores y capacitarlos |
| Estado en el teléfono | Un archivo local con el turno de la línea y la lista de acciones pendientes | Funcionar sin red. Al volver la señal, envía las acciones y descarga el estado |
| Avisos de relevo | `flutter_local_notifications`, con alarmas exactas | Que suene aunque el teléfono esté bloqueado o sin red |
| Escaneo | `mobile_scanner` | Escanear gafetes con la cámara |
| Acceso | `local_auth` y `flutter_secure_storage` | Entrar con PIN o huella, y guardar la sesión de forma segura |
| Etiquetas QR | `qr_flutter`, `pdf` y `printing` | Hoja de etiquetas en PDF para imprimir en la planta |
| Servidor | Supabase, plan gratuito: PostgreSQL, Auth, Storage, Realtime y Edge Functions | Base de datos, usuarios, fotos y cambios en tiempo real entre la línea y L8 (entre todas las líneas desde la versión 2) |
| Permisos | Row Level Security de PostgreSQL | Cada supervisor lee solo lo suyo. Enfermería ve solo las restricciones |
| Operaciones críticas | Funciones de PostgreSQL con actualizaciones condicionales | Que dos supervisores no se queden con la misma persona |
| Avisos entre supervisores | Firebase Cloud Messaging, enviado desde una función de Supabase. Además, el teléfono de L8 programa en local los envíos que ya conoce, para que suenen aunque se corte la red | Avisar a L8 de cada relevista que tiene que mandar (versión 1) y a cualquier supervisor de una solicitud (versión 2) |
| Carga inicial | Script en Dart que lee el Excel (paquete `excel`) | Pasar la planilla a la base de datos |
| Distribución | APK firmado en la propia computadora y guardado en Supabase Storage | Instalar sin Google Play. La app avisa cuando hay una versión nueva y la descarga |
| Repositorio | Git, en un repositorio privado de GitHub | Código y respaldo del código |
| Herramientas | Flutter SDK y Android Studio o VS Code, en Windows | Desarrollo y compilación |

```
smartassign/
├── app/        app Flutter para Android
├── core/       paquete Dart: reglas, reloj y motor de relevos
├── tools/      importador del Excel
└── supabase/   tablas, permisos y funciones
```

**Límites del plan gratuito de Supabase y cómo se manejan:**
- **Espacio:** 500 MB de base de datos y 1 GB de archivos. Alcanza de sobra: 164 fotos comprimidas ocupan menos de 10 MB.
- **Pausa por inactividad:** el proyecto se pausa después de una semana sin uso. Con uso diario no pasa. Si la planta para más de una semana, se reactiva desde el panel de Supabase sin costo.
- **Sin copias de seguridad automáticas:** por eso Configuración tiene "Exportar todo a Excel", y el administrador lo descarga una vez por semana.

**Teléfonos:** como no hay presupuesto para comprarlos, se usan los teléfonos Android de los supervisores, con su acuerdo. La app guarda lo mínimo y borra las fotos al cerrar el turno. Quien tenga iPhone queda fuera hasta que haya un Android disponible.

**Gafetes:** las etiquetas QR se imprimen en papel con la impresora de la planta y se pegan o se enmican. Mientras no estén, el supervisor escribe el número del gafete.

**Llave de firma:** el APK se firma con una llave que se genera una sola vez y se guarda en dos lugares seguros. Si se pierde, para actualizar la app habría que desinstalarla y volver a instalarla en cada teléfono.

**Lo que no se usa porque cuesta:** Google Play, Apple, compilación en la nube, actualizaciones automáticas de pago, planes de pago de Supabase, tablets y teléfonos nuevos.

**Reparto de responsabilidades:**
- **El teléfono** valida todas las reglas y calcula los relevos, porque tiene que hacerlo sin red.
- **El servidor** valida quién es dueño de cada persona y en qué estado está. Además vuelve a revisar las reglas duras (categoría, recuperación, habilitación, restricciones y liderazgo) en cada evento que recibe.
- **Las reglas duras quedan escritas dos veces,** en Dart y en SQL. Por eso hay **un solo juego de casos de prueba**, en un archivo compartido que se corre contra las dos. Si alguna vez no coinciden, la prueba falla. Es lo que faltó en el proyecto anterior, donde el servidor revisaba una sola restricción médica y el teléfono todas.
- **La hora del teléfono** se corrige con la del servidor en cada sincronización.

**Por qué no una PWA en los teléfonos:**
- Una PWA no puede programar un aviso que suene con el teléfono bloqueado y sin red. Sus avisos dependen del servidor, así que en una zona sin señal no llegan.
- En iPhone, además, solo recibe avisos si está agregada a la pantalla de inicio.
- Como los avisos de relevo son el centro de la herramienta, los teléfonos usan la app nativa.
- La versión web, que sale del mismo código, se puede instalar como PWA en una computadora para usar Configuración.

**Si sistemas no permite datos en la nube,** Supabase se puede instalar gratis en una computadora de la planta, con el mismo código.

**Riesgos técnicos:**
- **Avisos en Android:** hay que pedir el permiso de alarmas exactas y sacar la app del ahorro de batería. Cada marca (Samsung, Xiaomi, Motorola, etc.) lo hace distinto, y con teléfonos personales habrá varias. Si no se configura, los avisos pueden llegar con minutos de retraso. Se prueba la primera semana en cada teléfono.
- **Instalación fuera de Google Play:** Google anunció que irá exigiendo, país por país desde 2026, que las apps instaladas fuera de la tienda vengan de un desarrollador verificado. Antes de que aplique en el país de la planta hay que revisar qué pide y si tiene costo.

## 18. Orden de construcción

**Versión 1, para el piloto en una línea y L8.** L8 tiene que estar desde el principio, porque de ahí salen los relevistas.
1. **Núcleo y modo demostración.** Reglas, reloj y motor de relevos con pruebas automáticas que cubran los casos de este documento: recuperación de 24 h entre días, comida, paro largo, escalonado y reserva de L8. Los casos de las reglas duras van en el archivo compartido que después prueba también al servidor. Encima, el modo demostración, que corre un turno completo con reloj acelerado y los puestos reales de la planilla.
2. **Base de datos.** Tablas, permisos, funciones críticas, revisión de reglas duras en el servidor y carga desde el Excel con su reporte de problemas.
3. **App.**
   - Acceso, Configuración (personal y exportar a Excel), arranque y "Mi línea".
   - Escaneo, cola de relevos con avisos, salidas, paro, comida a las 12:00 y cierre de turno.
   - **Pantalla de L8:** a quién mandar, a qué línea y a qué hora, y quién está en camino de vuelta.
   - Pedidos de relevista a L8, traslado con llegada confirmada y vuelta a L8.
   - Aviso de versión nueva.
   - Lo de la propia línea funciona sin red.
4. **Piloto en una línea y L8,** en paralelo con el papel.

En la versión 1 el supervisor marca el doble turno a mano y elige los puestos activos sin plantilla de producto.

**Versión 2, antes de llevarla a todas las líneas:** préstamos entre cualquier par de líneas, gente ofrecida durante un paro, intercambio propuesto por la app y último recurso. En la versión 1 el intercambio se puede hacer a mano.

**Después:** Configuración en el navegador, comida escalonada, restricciones médicas (el campo queda listo desde la versión 1), plantillas de producto aprendidas, detección automática del doble turno y tablet en la línea.

## 19. Relación con el proyecto anterior

El proyecto anterior, en `Proyectos_Codigo\Gestion de Personal` (React, Capacitor y SQL Server), ya resolvió varios de estos problemas. De ahí se toma la lógica, no la tecnología.

**Lo que se adopta tal cual:**
- **Orden para proponer puesto al escanear:** primero el puesto del que es titular, después uno compatible, después uno compatible sin respetar el sexo preferente. Si no hay ninguno, la app explica qué regla lo impidió.
- **"Rastro dual" en los puestos fijos:** se guarda quién es el titular aunque lo cubra un B, para poder devolverle su máquina.
- **Vacante crítica:** la máquina sin titular ni B se destaca por encima de todo.
- **Pedir relevo sin liberar el puesto:** la persona sigue trabajando hasta que llega quien la releva.
- **Rechazos recordados por puesto** e **intercambio** entre dos personas de la línea.
- **Ventana de arranque** de 10 minutos, con gente solo de la propia línea.
- **Paros clasificados en dos niveles:** categoría y causa concreta.
- **Textos que explican cada puesto** y **toda negativa con su causa y el siguiente paso.**
- **"No se usa hoy" es distinto de libre y de ocupado.** En el proyecto anterior, confundirlos hizo que una línea sin arrancar apareciera como "línea completa".
- **Sin doble toque,** y "cargando" distinto de "vacío".
- **Fotos reales en la confirmación,** porque las iniciales no sirven para verificar identidad.
- **No importar más datos personales de los necesarios.**

**Lo que se toma y se mejora:**

| Tema | Proyecto anterior | Este diseño | Por qué |
|---|---|---|---|
| Bolsón | Todo relevista sale de L8 y todo relevado vuelve a L8 | Igual. Además, la app pide cada relevo a L8 con la anticipación de la caminata y avisa antes si L8 se va a quedar corta | En la planta no hay gente libre: todos están en un puesto o en L8. Y L8 es la línea más lejana desde todas las demás, así que sin anticipación el relevo llega tarde |
| Efecto dominó | Todo movimiento entre líneas pasa por L8 | Igual, con dos excepciones controladas (paro y último recurso). El hueco que deja el último recurso solo se cubre desde L8 | Un solo salto por movimiento, siempre explicable |
| Sin gente en L8 | Se toma de la línea de menor prioridad, sin bajar de un mínimo | Igual, y antes la app prueba un intercambio dentro de la línea, que no hace caminar a nadie | Evita abrir un hueco en otra línea cuando se puede resolver adentro |
| Recuperación | Solo la regla de 24 horas: no repetir la tarea con que terminó el día anterior | Recuperación por tipo de puesto con los tiempos de la planilla (de 1 a 48 h), que ya incluye la regla de 24 horas | La planilla tiene esos tiempos y el proyecto anterior no los usaba |
| Relevos | Manuales: el supervisor pide el relevo y la app busca a alguien | La app los anticipa, los escalona y avisa. Pedir un relevo a mano sigue siendo posible | Es lo que pide el problema: que algo avise antes del límite |
| Titulares al arrancar | Barrido automático por el checador, que lanza el coordinador | Escaneo, o un toque en su máquina | No hay coordinador ni conexión con el checador |
| Escaneo rápido | Un modo continuo que asigna sin confirmar | Cámara abierta, pero siempre con un toque de confirmación sobre la foto | Saltarse la confirmación anula la protección contra gafetes prestados |
| Paros | Los operarios de puestos varios se van siempre a L8 | Depende de la duración: si el paro es corto se quedan con el reloj en pausa; si es largo, la app propone mandarlos a L8 u ofrecerlos | Caminar hasta L8 por un paro de 10 minutos cuesta más de lo que rinde |
| Sin red | Bloquea todas las asignaciones | Bloquea lo que involucra a otra línea. Cada supervisor sigue operando con su gente, y el servidor revisa las reglas al sincronizar | Un relevo que ya pasó en la línea no se puede deshacer, así que es mejor registrarlo y revisarlo después |
| Validación | Servidor y teléfono con reglas distintas: el servidor revisaba una sola restricción médica | Las mismas reglas en los dos lados, con un juego de casos de prueba compartido | Que no vuelva a pasar que la protección real dependa del teléfono |
| Restricciones médicas | El supervisor las ve | El supervisor solo ve "apto" o "no apto" | Privacidad |
| Carga de datos | Varios scripts de migración y siembra | Un solo script que, antes de cargar, genera un reporte de problemas para corregir en el Excel | La planilla tiene errores conocidos (sección 3) |
