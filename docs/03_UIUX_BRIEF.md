# SmartAssign — UI/UX Design Brief

**Sistema de diseño, componentes, accesibilidad y layout para una app que se usa de pie y en movimiento.**
Versión 1.0 · 2026-08-09

> **De dónde parte este documento.** No de una tendencia visual, sino de las condiciones de uso reales que describe §12.3: **una mano, de pie, en movimiento, bajo iluminación industrial variable, con la pantalla a brillo parcial y el protector rayado**. El uso con guantes quedó descartado por el cliente *(A11)*; el resto del §12.3 sigue íntegro. Cada decisión de abajo se justifica contra esa realidad o contra una regla explícita de la especificación.
>
> **La micro-copia del §12.5 es literal y no se reinventa.** Está transcrita en §7 de este documento marcada como intocable.

---

# 1 · Principios de diseño

Cinco principios, todos derivados de la fuente. Cuando dos decisiones visuales compitan, gana la que respete el principio de número más bajo.

| # | Principio | Origen | Consecuencia práctica |
|---|---|---|---|
| **1** | **El sistema nunca miente** | §1.3, §12.4 | Cargando ≠ vacío ≠ fuera de operación ≠ error. Cuatro tratamientos visuales distintos, siempre. |
| **2** | **Legible en el peor caso, no en el mejor** | §12.3 | El contraste se valida con brillo parcial y mica rayada, no con la pantalla nueva en interiores. |
| **3** | **Se toca sin mirar fijamente** | §12.3, A11 | De pie y en movimiento: zonas de toque holgadas y separadas, aunque el dedo vaya desnudo. |
| **4** | **Nunca solo color** | §12.2 | Todo estado se comunica con **texto + forma + color**. El color es el tercer canal, nunca el único. |
| **5** | **La acción frecuente vive bajo el pulgar** | §12.3 | Las acciones críticas van en el tercio inferior. La información va arriba. |

---

# 2 · Sistema de diseño (Design System)

## 2.1 Tokens de color

Definidos como tokens semánticos, no como nombres de color. Un token dice **para qué sirve**, no de qué color es — así el ajuste de contraste no obliga a renombrar nada.

### Base

| Token | Valor | Uso |
|---|---|---|
| `color.bg.base` | `#0E1116` | Fondo de la app |
| `color.bg.surface` | `#171C23` | Tarjetas, hojas, paneles |
| `color.bg.surface.raised` | `#212832` | Tarjeta seleccionada, modal |
| `color.bg.overlay` | `#000000` @ 72 % | Fondo de modal |
| `color.border.subtle` | `#2C3540` | Separadores |
| `color.border.strong` | `#455161` | Borde de componente interactivo |
| `color.text.primary` | `#F4F7FA` | Texto principal |
| `color.text.secondary` | `#B4BFCC` | Texto de apoyo, micro-copia |
| `color.text.disabled` | `#6B7684` | Solo texto no accionable |

> **Por qué tema oscuro como base:** la planta tiene iluminación variable y el operador mira la pantalla decenas de veces por turno a distancia corta. Un fondo oscuro con texto claro reduce el deslumbramiento en zonas de poca luz y baja el consumo en pantallas OLED. **Se valida obligatoriamente también bajo luz directa**, que es el caso adverso del tema oscuro — ver §5.2.

### Estados de puesto *(§5.3, C11)*

Cinco estados que **nunca** pueden confundirse entre sí. Cada uno tiene color, forma de borde e icono propios.

| Estado | Token | Color | Forma distintiva | Icono |
|---|---|---|---|---|
| **Libre** | `color.state.libre` | `#4A90D9` | Borde discontinuo | Contorno vacío |
| **Ocupado** | `color.state.ocupado` | `#3FA76A` | Borde sólido | Silueta llena |
| **Vacante crítica** | `color.state.critico` | `#E5484D` | Borde sólido grueso + franja lateral | Triángulo de alerta |
| **Rotativo descubierto** | `color.state.descubierto` | `#D9822B` | Borde discontinuo grueso | Contorno con punto |
| **Fuera de operación** | `color.state.fuera` | `#5A6472` | Sin borde, superficie hundida, 55 % opacidad | Diagonal tachada |

> **Regla dura de este brief** *(§5.3)*: *"Fuera de operación" no es "libre" ni "ocupado". Es una tercera categoría.* Si el diseño la confunde con "ocupado", la línea parecerá llena estando vacía. Si la confunde con "libre", se contará déficit donde la cobertura está completa. Por eso es el único estado con **superficie hundida y opacidad reducida**: se lee como "esto no existe hoy" antes incluso de leer el texto.

### Niveles de fatiga *(§9.1)*

| Nivel | Token | Color | Forma |
|---|---|---|---|
| **Normal** | `color.fatiga.normal` | `#3FA76A` | Barra fina |
| **Relevo sugerido** | `color.fatiga.sugerido` | `#D9822B` | Barra media + icono de reloj |
| **Relevo crítico** | `color.fatiga.critico` | `#E5484D` | Barra gruesa + icono de alerta + pulso lento |

> **La barra es relativa, no absoluta** *(A4)*. Cada puesto tiene su propio umbral, así que la barra representa **porcentaje sobre el umbral propio del puesto**, no minutos. Dos puestos con 70 minutos pueden mostrar barras muy distintas, y eso es correcto.
>
> **El avance debe verse de forma continua** *(§9.1)*, no solo al cruzar: la barra se llena progresivamente desde el minuto cero para que el supervisor pueda anticiparse.

### Semánticos de sistema

| Token | Color | Uso |
|---|---|---|
| `color.accion.primaria` | `#145DEB` | Botón de acción principal *(A15)* |
| `color.exito` | `#3FA76A` | Confirmación completada |
| `color.alerta` | `#D9822B` | Advertencia no bloqueante |
| `color.peligro` | `#E5484D` | Rechazo, restricción médica, paro |
| `color.medico` | `#B5179E` | **Exclusivo de restricciones médicas** |
| `color.offline` | `#8A6D3B` sobre `#2B2113` | Banner de sin conexión |
| `color.transito` | `#7B5CD6` | Persona en tránsito, puesto reservado |

> **Por qué las restricciones médicas tienen color propio** *(§7.2, §12.2)*: es la única información de la app cuyo malentendido produce daño físico a una persona. No comparte color con ninguna otra alerta para que nunca se confunda con un aviso operativo ordinario. Y aun así **nunca se comunica solo con ese color**: siempre lleva icono y texto explícito (principio 4).

> **A15 · Por qué `color.accion.primaria` se oscureció de `#2F6FED` a `#145DEB`.** Mismo tono (220°) y misma saturación (84 %), solo más profundo. El azul original dejaba el rótulo del botón en **4.19:1**, por debajo de los 4.5:1 que §5.2 pide incluso a texto grande. La corrección no pudo ser solo cromática: sobre `bg.base` (`#0E1116`, luminancia 0.0055) las dos reglas de §5.2 **se excluyen entre sí** — para que el texto alcance 7:1 el azul necesita luminancia ≤ 0.100 (aun con blanco puro), y para que el botón se distinga del fondo a 3:1 necesita ≥ 0.1166. *Ningún color cumple las dos.* `#145DEB` es el punto que maximiza el contraste del rótulo (**5.15:1**) conservando **3.42:1** de botón contra fondo; el resto del camino lo hace `type.action` (§2.2), que lleva el rótulo al rango de texto grande. Un azul más oscuro haría el texto más legible dentro de un botón que se pierde contra el fondo — cambiar un problema de accesibilidad por otro.

## 2.2 Escala tipográfica

Fuente: **Roboto** (sistema Android, sin descarga externa — §12.1 prohíbe depender de recursos externos).

| Token | Tamaño | Peso | Uso |
|---|---|---|---|
| `type.display` | 34 sp | 700 | Cronómetro de paro, cifra de eficiencia |
| `type.title` | 26 sp | 700 | Título de pantalla, nombre de puesto |
| `type.subtitle` | 20 sp | 600 | Nombre de persona en tarjeta |
| `type.body` | 18 sp | 400 | Texto general |
| `type.body.strong` | 18 sp | 600 | Dato que se lee de un vistazo |
| `type.action` | 24 sp | 600 | **Rótulo de botón** *(A15)* |
| `type.caption` | 16 sp | 400 | Micro-copia contextual |
| `type.label` | 15 sp | 600 | Etiquetas, todo en mayúsculas |
| `type.mono` | 18 sp | 500 | Número de ficha, identificador de puesto |

> **Por qué el cuerpo es 18 sp y no los 14–16 habituales** *(§12.3)*: se lee de pie, en movimiento, a distancia de brazo, con la pantalla a brillo parcial. **No se define ningún tamaño por debajo de 15 sp** en toda la aplicación. Si un contenido no cabe a 15 sp, el problema es el contenido, no el tamaño.
>
> **A15 · Por qué el rótulo de botón tiene token propio a 24 sp.** Dos razones que apuntan al mismo sitio. **Contraste:** §5.2 fija 4.5:1 para texto ≥ 24 sp y 7:1 para el resto; sobre `color.accion.primaria` el 7:1 es inalcanzable sin que el botón desaparezca contra el fondo (ver la nota de A15 en §2.1), así que el rótulo entra al rango de texto grande — donde 5.15:1 cumple con holgura. **Proporción:** §5.1 deja la acción primaria en 64 dp *"porque es la que más se usa y la que menos puede fallar"*, y este mismo apartado justifica el cuerpo en 18 sp porque se lee de pie y en movimiento. Antes de A15 el rótulo usaba `type.caption` (16 sp): el control más importante de la aplicación llevaba el penúltimo tamaño de la escala, lo que contradecía ese razonamiento.
>
> El escalado tipográfico del sistema se respeta hasta el 130 % sin que se rompa ningún layout.

## 2.3 Espaciado, radios y elevación

Escala base **4 dp**.

| Token | Valor | Uso |
|---|---|---|
| `space.xs` | 4 dp | Separación interna mínima |
| `space.sm` | 8 dp | Entre elementos relacionados |
| `space.md` | 16 dp | Padding interno de tarjeta |
| `space.lg` | 24 dp | Entre bloques |
| `space.xl` | 32 dp | Margen superior de sección |
| `space.touch` | **8 dp** | **Separación mínima entre dos zonas de toque** |
| `radius.sm` / `md` / `lg` / `pill` | 8 / 12 / 20 / 999 dp | Chip / tarjeta / hoja / badge |
| `elevation.0/1/2/3` | 0 / 2 / 6 / 12 dp | Base / tarjeta / hoja / modal |

> **`space.touch` es una regla, no una sugerencia** *(§12.3, A11)*: dos acciones destructivas o irreversibles nunca quedan a menos de 8 dp una de otra. Se toca caminando y mirando la línea, no la pantalla.

---

# 3 · Biblioteca de componentes

Cada componente se especifica con todos sus estados. **Ningún componente puede existir sin estado `loading` y sin estado `disabled` con razón visible.**

## 3.1 Tarjeta de puesto — el componente central

Es el elemento más usado de la app. Aparece en la malla de línea, repetido entre 8 y 20 veces por pantalla.

```
┌────────────────────────────────────────────────┐
│ ▌ PUESTO 3            Rotativo          ⟳ 78% │  ← franja lateral = estado
│ ▌                                              │
│ ▌ MARÍA LÓPEZ HERNÁNDEZ                        │  type.subtitle
│ ▌ Ficha 4821 · Operario            ⚕ 2         │  ← indicador médico
│ ▌                                              │
│ ▌ ████████████████████░░░░░  Relevo sugerido   │  ← barra relativa
│ ▌ Relevo sugerido — 62 minutos en el puesto    │  ← micro-copia §12.5
└────────────────────────────────────────────────┘
   ↑ 4 dp de franja lateral con el color del estado
```

**Anatomía obligatoria:**
1. **Franja lateral** de 4 dp con el color del estado — legible en visión periférica sin leer nada.
2. **Identificador de puesto** en `type.mono`.
3. **Tipo de puesto**: fijo o rotativo, siempre visible (gobierna toda la lógica, §5).
4. **Ocupante** o el estado vacío correspondiente.
5. **Indicador médico** `⚕ N` si la persona tiene restricciones activas.
6. **Barra de fatiga** solo en rotativos ocupados *(§9.1)*.
7. **Micro-copia contextual** *(§12.5)* — siempre presente, nunca opcional.

**Estados del componente:**

| Estado | Tratamiento |
|---|---|
| `default` | Según estado de puesto (§2.1) |
| `pressed` | Superficie a `surface.raised`, escala 0.98, sin retardo perceptible |
| `disabled` | 45 % opacidad **+ razón visible debajo**. Nunca gris mudo |
| `loading` | Esqueleto con la forma exacta de la tarjeta, pulso lento |
| `offline` | Marca de agua diagonal + leyenda del §12.1 |
| `reservado` | Borde `color.transito` + *"Reservado — [Nombre] viene en camino"* |
| `demorado` | Borde `color.transito` punteado + *"Relevista demorado"* (B11) |

> **Regla:** un componente deshabilitado **siempre dice por qué**. Un botón gris sin explicación entrena al supervisor a pensar que la app está rota (§1.3).

## 3.2 Tarjeta de persona

Usada en búsqueda manual, personal de línea, recepciones pendientes y cola del Bolsón.

```
┌────────────────────────────────────────────────┐
│  MARÍA LÓPEZ HERNÁNDEZ              ⚕ 2   ⏱⏱  │  ← ⏱⏱ = doble turno (§11.5)
│  Ficha 4821 · Operario                         │
│  En Bolsón · 34 min                            │
│  ◆ En tu línea                                 │  ← prioridad de §12.2
└────────────────────────────────────────────────┘
```

**Reglas de contenido:**
- El distintivo de **doble turno** es permanente y aparece en toda pantalla donde figure la persona *(§11.5, B7)*.
- El indicador `⚕` aparece **siempre** que haya restricciones activas, aunque no sean relevantes para el puesto actual.
- En búsqueda manual, **solo personal disponible**, y **primero los que están físicamente en la línea del supervisor** *(§12.2)*, con el marcador `◆ En tu línea`.

## 3.3 Modal de confirmación de identidad — componente de seguridad

**Este componente es una regla dura con forma de pantalla** *(§12.2)*. Su diseño no es negociable.

```
┌──────────────────────────────────────────────┐
│                                              │
│   ¿Es esta la persona?                       │
│                                              │
│   MARÍA LÓPEZ HERNÁNDEZ                      │  type.title
│   Ficha 4821                                 │  type.mono
│   Operario                                   │
│                                              │
│  ┌────────────────────────────────────────┐  │
│  │ ⚕ RESTRICCIONES MÉDICAS ACTIVAS        │  │  ← color.medico
│  │                                        │  │     borde grueso
│  │ · No levantar carga superior a 10 kg   │  │     nunca colapsable
│  │ · No bipedestación prolongada          │  │
│  └────────────────────────────────────────┘  │
│                                              │
│   Destino: Puesto 3 · Rotativo               │
│                                              │
│   ┌──────────────────────────────────────┐   │
│   │      CONFIRMAR ASIGNACIÓN            │   │  ← 64 dp
│   └──────────────────────────────────────┘   │
│   ┌──────────────────────────────────────┐   │
│   │             Cancelar                 │   │  ← 56 dp
│   └──────────────────────────────────────┘   │
└──────────────────────────────────────────────┘
```

**Reglas del componente:**
1. **El bloque médico nunca se colapsa, nunca se abrevia, nunca aparece bajo un "ver más".**
2. Si **no** hay restricciones, el bloque aparece igual con *"Sin restricciones médicas registradas"* — la ausencia de información se afirma, no se omite. *(§12.4)*
3. **El escaneo por sí solo nunca asienta la asignación** *(§12.2)*. Este modal siempre media.
4. El botón de confirmar **no está preseleccionado** y no responde a un toque accidental de retorno.
5. Aparece igual **con y sin conexión**, gracias a la caché cifrada *(D3)*.

> **Por qué tanto rigor** *(§12.2)*: los gafetes se prestan, se confunden y se intercambian. Asignar a la persona equivocada significa que alguien con una restricción médica termina en un puesto que su condición prohíbe.

## 3.4 Botones

| Variante | Altura | Uso |
|---|---|---|
| **Primario** | **64 dp** | Acción principal de la pantalla. Uno solo por pantalla |
| **Secundario** | 56 dp | Acción alternativa |
| **Terciario** | 48 dp | Navegación, acciones no destructivas |
| **Destructivo** | 64 dp | Liberar, retirar, rechazar. `color.peligro` + icono + confirmación |

Estados: `default` · `pressed` (escala 0.97 + oscurecido) · `disabled` (45 % + razón visible) · `loading` (**bloqueado contra doble toque**, indicador en el propio botón, §12.4).

> **No existe estado `hover`.** Es una app táctil sin puntero. Incluirlo sería copiar un patrón de escritorio sin sentido aquí.

## 3.5 Campo de búsqueda

- Altura **64 dp**, texto `type.body`.
- **Teclado numérico por defecto** — la ficha es el identificador que más se teclea, y las teclas grandes reducen el error al escribir con una mano.
- Botón de limpiar de **48 dp**.
- Resultados en tarjetas de persona (§3.2), con los de la propia línea primero *(§12.2)*.
- Estado vacío: *"Sin resultados para «4821». Revisa el número o busca por nombre."* — nunca una lista en blanco *(§12.4)*.

## 3.6 Banner de conexión

```
┌────────────────────────────────────────────────┐
│ ⚠  SIN CONEXIÓN                                │
│    Pendiente de sincronización — no mover al   │
│    personal hasta recuperar la red.            │
└────────────────────────────────────────────────┘
```

- **Fijo en la parte superior**, no descartable, sobre todas las pantallas *(§12.1)*.
- Texto literal de §12.1.
- Ocupa altura real y **empuja el contenido**: no lo tapa, para que no oculte un puesto.

## 3.7 Sello de frescura *(D4)*

Línea discreta bajo la cabecera: `Datos de hace 2 min`.

- Bajo `antiguedad_maxima`: `color.text.secondary`, sin llamar la atención.
- Por encima: pasa a `color.alerta`, y el contenido de datos se muestra al **60 % de opacidad** con la marca de agua diagonal.

> **Por qué degradar visualmente y no solo avisar** *(§12.4)*: un aviso se ignora; un contenido que se ve distinto no. El objetivo es que sea físicamente imposible confundir dato viejo con dato vivo.

## 3.8 Cronómetro de paro *(§11.1)*

```
┌────────────────────────────────────────────────┐
│ ⏱ PARO · MECÁNICO           00:14:32           │
└────────────────────────────────────────────────┘
```

- **Persistente y visible en todo momento**, aunque el supervisor navegue a otras partes de la aplicación *(§11.1)*.
- Barra fija bajo la cabecera, por debajo del banner de conexión si ambos están activos.
- Tipografía `type.display` en tabular, para que los dígitos no bailen.
- Solo desaparece al **reanudar producción** explícitamente.

## 3.9 Indicador de estadística *(C4)*

```
┌──────────────────────┐
│ EFICIENCIA           │
│ 87 %                 │  ← type.display, color por tramo
│ ███████████░░░  Óptimo│
│ hace 1 min            │  ← sello de frescura SIEMPRE
└──────────────────────┘
```

- Tres tramos con umbrales configurables *(§11.4)*: óptimo / aceptable / crítico.
- **Siempre con sello de última actualización.**
- Si no hay registro reciente: *"Estimada desde el último registro — hace N min"* *(C4)*.
- **Nunca muestra un número sin respaldo.** Si no hay dato, dice *"Sin datos de producción todavía"*.

## 3.10 Chips de estado y filtro

`radius.pill`, altura **44 dp**, siempre **icono + texto**, nunca solo color (principio 4).

## 3.11 Los cuatro estados de pantalla *(§12.4)*

`[REGLA DURA]` de interfaz.

| Estado | Tratamiento |
|---|---|
| **Cargando** | **Esqueleto con la forma del contenido real**, pulso 1.2 s. Nunca pantalla en blanco, nunca solo un círculo girando |
| **Vacío legítimo** | Icono + frase que explica + siguiente paso. *"Ningún puesto en fatiga ahora mismo."* |
| **Fuera de operación** | Tratamiento propio, ni libre ni ocupado. Superficie hundida + diagonal |
| **Error** | Causa concreta + acción concreta. *"No se pudo cargar la línea. Reintentando en 5 s."* Nunca código |

> **La distinción entre cargando y vacío es la más importante de este documento** *(§12.4)*: una línea que aún no responde y una línea sin nadie asignado se ven igual si no se distinguen, y eso lleva al supervisor a reasignar personal que ya estaba colocado.
>
> Por eso el esqueleto **tiene la forma del contenido real**: 12 tarjetas de puesto pulsando se leen instantáneamente como "esto viene en camino", mientras que un círculo girando en medio de la pantalla no dice nada sobre qué se está cargando.

---

# 4 · Grillas y layout

## 4.1 Grilla base

| Parámetro | Valor |
|---|---|
| Columnas | 4 |
| Margen lateral | 16 dp |
| Canal entre columnas | 12 dp |
| Ancho de referencia | 360 dp |

## 4.2 Zonas de la pantalla — el mapa del pulgar

```
┌─────────────────────────────────┐  0
│  BANNER (offline / paro)        │  ← fijo, no accionable
├─────────────────────────────────┤  ~88 dp
│  CABECERA                       │
│  línea · turno · sello frescura │  ← informativa
├─────────────────────────────────┤  ~180 dp
│                                 │
│  ZONA DE LECTURA                │
│  malla de puestos, listas       │  ← desplazable
│                                 │     alcance medio
│                                 │
├─────────────────────────────────┤  ~65 % de altura
│                                 │
│  ZONA DE ACCIÓN                 │  ← ALCANCE CÓMODO
│  acciones frecuentes            │     DEL PULGAR
│                                 │
├─────────────────────────────────┤
│  BARRA INFERIOR                 │  ← acción primaria
└─────────────────────────────────┘  altura total
```

> **Regla de layout** *(§12.3)*: *"Las acciones frecuentes deben estar al alcance del pulgar."*
>
> - **Ninguna acción frecuente en el tercio superior.** La parte alta es para leer, no para tocar.
> - La **acción primaria** de cada pantalla vive en la barra inferior, a ancho completo.
> - La navegación de retorno se duplica con **gesto de deslizamiento**, porque la flecha de la esquina superior izquierda es el punto menos alcanzable con una mano.

## 4.3 Malla de puestos

- **Una columna** en tarjetas de ancho completo. No dos.

> **Por qué una sola columna:** dos columnas duplican la densidad, pero obligan a abreviar la micro-copia del §12.5 —que es obligatoria— y reducen la zona de toque justo cuando se está leyendo de pie. La densidad es menos valiosa que acertar el toque a la primera.

- Agrupada por tipo: **fijos primero**, rotativos después. Los fijos gobiernan si la línea puede operar.
- Dentro de cada grupo, orden fijo por identificador de puesto. **El orden nunca cambia solo**: un puesto que se mueve de sitio entre dos miradas obliga a releer toda la pantalla.
- Los **fuera de operación** se agrupan al final, colapsados tras *"3 puestos no requeridos por el SKU de hoy"*.

## 4.4 Panel del Coordinador — las 10 líneas

```
┌─────────────────────────────────┐
│  PLANTA · Turno A · hace 30 s   │
├─────────────────────────────────┤
│ ▌L4  ●●●●●●●●○○  8/10   87% ⚠2 │
│ ▌L1  ●●●●●●●●●●  10/10  92%    │
│ ▌L2  ⏸ EN PARO 00:14:32        │
│ ▌L3  ○ INACTIVA                 │
│ ...                             │
├─────────────────────────────────┤
│  PLANTA: 142/160 · 3 alertas    │
└─────────────────────────────────┘
```

- Una fila por línea, altura **72 dp**, tocable entera.
- Cobertura como **puntos discretos**, no como barra: se cuentan de un vistazo y no mienten por redondeo.
- La franja lateral lleva el estado de la línea.
- Fila inferior con el agregado de planta.
- **Sello de frescura del panel completo** *(C4, D4)*.

> **El Coordinador también opera desde teléfono** *(F4)*, con las mismas condiciones del §12.3. No hay una versión de escritorio ni una consola web donde refugiar lo que no cabe: §2.1.10 exige que **todo** dato maestro se edite desde la aplicación.

## 4.6 Datos maestros con el pulgar *(F4, §2.1.10)*

El Coordinador administra volúmenes que normalmente vivirían en una tabla de escritorio. Cada uno necesita una interacción táctil propia — copiar una hoja de cálculo a una pantalla de 360 dp no funciona.

| Dato | Volumen | Interacción |
|---|---|---|
| **Padrón** *(§2.1.6)* | ~160 personas | **Búsqueda primero.** La pantalla abre en el campo de búsqueda, no en una lista. Se entra por ficha o nombre; el listado completo es el último recurso, no el primero |
| **Prioridad de líneas** *(§2.1.3, B8)* | 10 elementos | **Lista reordenable arrastrando.** Es exactamente lo que la interacción táctil hace bien: 10 filas de 72 dp con asa de arrastre a la derecha, dentro del alcance del pulgar |
| **Proximidad** *(A1, A3)* | **10 × 9 = 90 posiciones** | ⚠ **Nunca como cuadrícula.** Se elige una línea de origen y se reordenan arrastrando sus 9 destinos. Es la misma interacción que la prioridad, repetida diez veces |
| **Puestos** | ~300 | Agrupados por línea, plegados. Se despliega una línea a la vez |
| **SKU, turnos, catálogos** | Decenas | Lista con búsqueda + hoja de edición inferior |
| **Parámetros** *(§12.6)* | ~13 | Lista de fichas con valor actual visible y edición en hoja inferior |

### Por qué la proximidad no se dibuja como matriz

Una cuadrícula de 90 celdas en 360 dp da celdas de unos 36 dp: **por debajo del mínimo de 48 dp** de §5.1, e ilegible sin zoom.

Y hay una razón más fuerte que el tamaño: **la proximidad es un grafo dirigido y asimétrico** *(A3)*. Una matriz sugiere visualmente que la celda (L1, L5) y la celda (L5, L1) son la misma cosa, y **no lo son**. Presentarla como diez listas ordenadas —una por línea de origen— refleja el modelo real y hace evidente que cada línea tiene su propio recorrido.

```
┌─────────────────────────────────┐
│  PROXIMIDAD · Origen: L10       │  ← selector de línea de origen
├─────────────────────────────────┤
│  1   L9                    ⣿    │  ← asa de arrastre
│  2   L3                    ⣿    │
│  3   L6                    ⣿    │
│  4   L7                    ⣿    │
│  ...                            │
│  9   L8                    ⣿    │
├─────────────────────────────────┤
│         [ Guardar orden ]       │
└─────────────────────────────────┘
```

### Pantalla de alta de dispositivo *(F3)*

El Coordinador genera el QR con la URL del servidor. Requisitos:

- QR **a pantalla completa**, con brillo forzado al máximo mientras está visible — se va a escanear con otro teléfono bajo iluminación industrial.
- **La URL también en texto legible** debajo, como respaldo si la cámara del otro dispositivo falla.
- Sin datos sensibles en el código: **solo la URL del servidor**.

## 4.5 Panel del Bolsón *(C7)*

**No tiene malla de puestos.** Su estructura es distinta:

```
┌─────────────────────────────────┐
│  L8 · BOLSÓN · hace 15 s        │
├─────────────────────────────────┤
│  ⚡ RECEPCIONES PENDIENTES  (12) │  ← primero: gente esperando
├─────────────────────────────────┤
│  🔄 COLA DE RELEVOS         (5)  │
│     L4·P3 crítico 118%          │
│     L1·P7 sugerido 104%         │
├─────────────────────────────────┤
│  👥 PERSONAL EN BOLSÓN      (23) │
└─────────────────────────────────┘
```

> **Las recepciones van arriba del todo** *(C8)*: la confirmación es individual, persona por persona, así que puede haber una docena de personas esperando físicamente a que alguien las confirme. Es lo más urgente de esa pantalla y por eso ocupa el primer bloque.

**Pantalla de recepciones — mitigación de fricción** *(C8)*:

```
┌─────────────────────────────────┐
│  RECEPCIONES PENDIENTES  12     │
├─────────────────────────────────┤
│ MARÍA LÓPEZ  4821    [CONFIRMAR]│  ← 72 dp, un solo toque
├─────────────────────────────────┤
│ JUAN PÉREZ   3910    [CONFIRMAR]│
├─────────────────────────────────┤
│ ...                              │
└─────────────────────────────────┘
```

- **Un solo toque por persona, sin modal intermedio.**
- Contador visible de lo que falta.
- Nombre y ficha en la tarjeta, suficientes para verificar quién llegó.

> **Proporcionalidad de la verificación** *(C8)*: recibir en el Bolsón no asigna ningún puesto, así que basta la tarjeta. El modal completo del §12.2 —con categoría y restricciones médicas— se exige **al asignar a un puesto**, que es cuando una identidad equivocada tiene consecuencia ocupacional.

---

# 5 · Usabilidad y accesibilidad

## 5.1 Zonas de toque

| Elemento | Mínimo | Justificación |
|---|---|---|
| **Absoluto, cualquier elemento** | **48 dp** | Mínimo estándar de Android *(A11)* |
| Acción primaria | 64 dp | Es la que más se usa y la que menos puede fallar |
| Fila de lista tocable | 72 dp | Se toca en movimiento |
| Separación entre zonas de toque | 8 dp | Se toca de pie y en movimiento |
| Separación entre acciones destructivas | 24 dp | Muy por encima del resto, porque el error no se deshace |

> **48 dp de piso, 64 dp en la acción primaria** *(A11)*. El cliente descartó el uso con guantes, así que el piso vuelve al mínimo estándar de Android. Lo que **no** se relaja es la acción primaria: sigue en 64 dp porque es la que más se usa y la que menos puede fallar — estas operaciones no son repetibles sin consecuencia *(§12.4)*.

## 5.2 Contraste

| Contenido | Ratio mínimo | Estándar |
|---|---|---|
| Texto normal | **7:1** | WCAG **AAA** |
| Texto grande (≥ 24 sp) | **4.5:1** | WCAG AA reforzado |
| Elementos de interfaz y bordes | **3:1** | WCAG AA |
| **Restricciones médicas** | **7:1** + icono + texto | Nunca solo color |

> **Por qué AAA y no AA** *(§12.3)*: *"Se opera bajo iluminación industrial variable, con la pantalla a brillo parcial y el protector rayado. El contraste debe funcionar en el peor caso."* AA se valida en condiciones de oficina. Aquí el punto de partida es una pantalla rayada al 40 % de brillo bajo un tubo fluorescente parpadeante.

**Validación obligatoria antes de aprobar cualquier pantalla:**
1. Medición automática de ratios sobre todos los pares de tokens.
2. Prueba física: teléfono al **40 % de brillo**, con protector usado, bajo la iluminación real de planta.
3. Prueba bajo **luz directa** — el caso adverso del tema oscuro.

## 5.3 Nunca solo color *(§12.2)*

> *"El resultado de cada validación debe comunicarse con texto y forma, no solo con color."*

Toda información de estado lleva **tres canales simultáneos**:

| Canal | Ejemplo en "relevo crítico" |
|---|---|
| **Color** | Rojo `color.fatiga.critico` |
| **Forma** | Barra gruesa + icono de alerta + franja lateral ancha |
| **Texto** | *"Límite ergonómico superado — 94 minutos en el puesto"* |

Consecuencia verificable: **la app debe ser completamente operable en escala de grises.** Es la prueba de aceptación de este principio, y cubre de paso el daltonismo, que afecta a alrededor del 8 % de los hombres.

## 5.4 Operación con una mano

- Acción primaria siempre en la barra inferior.
- Retorno duplicado con **gesto de deslizamiento desde el borde**.
- Ninguna acción crítica en las esquinas superiores.
- Los modales anclan sus botones **abajo**, no arriba.
- Ningún gesto obligatorio con dos dedos. El zoom es opcional, nunca la única vía.

## 5.5 Retroalimentación

| Situación | Respuesta |
|---|---|
| Toque válido | Cambio visual inmediato + vibración corta (10 ms) |
| Acción en curso | **Botón bloqueado** + indicador en el propio botón *(§12.4)* |
| Éxito | Confirmación visible ≥ 1.5 s + vibración doble |
| Rechazo | Mensaje persistente con causa y siguiente paso + vibración larga |
| Restricción médica bloqueante | Mensaje persistente **que exige descarte explícito**. No desaparece solo |

> **Por qué el bloqueo contra doble toque es obligatorio** *(§12.4)*: sin retroalimentación, el reflejo ante la demora es volver a tocar. Y estas operaciones no son repetibles sin consecuencia: se piden dos relevos, se despacha dos veces, se registra la baja dos veces.
>
> **Por qué el rechazo médico no se descarta solo:** es el único mensaje de la app cuyo ignorarlo tiene consecuencia física. Se queda hasta que el supervisor lo descarta con un toque deliberado.

## 5.6 Accesibilidad de sistema

- Todo elemento interactivo con etiqueta de accesibilidad descriptiva.
- Estados anunciados por lector: *"Puesto 3, ocupado por María López, relevo sugerido"*.
- Escalado de fuente del sistema hasta 130 % sin romper layout.
- Sin animaciones esenciales para comprender: se respeta la preferencia de movimiento reducido.
- Contenido de la malla navegable en orden lógico, no visual.

---

# 6 · Reglas de comportamiento visual

## 6.1 Lo que la interfaz nunca hace

| Prohibición | Origen |
|---|---|
| Mostrar una pantalla vacía sin explicación | §12.4 |
| Presentar "cargando" y "vacío" con el mismo aspecto | §12.4 |
| Comunicar un estado solo con color | §12.2 |
| Mostrar un botón deshabilitado sin razón visible | §1.3 |
| Permitir doble toque en una operación no repetible | §12.4 |
| Mostrar un código de error o texto genérico | §12.4 |
| Colapsar u ocultar restricciones médicas | §12.2 |
| Asentar una asignación solo con el escaneo | §12.2 |
| Mostrar dato sin conexión como si fuera vivo | D4 |
| Mostrar identidad de personal ajeno a un supervisor | §2.2, D1, D2 |
| Sugerir que una operación se ejecutó cuando no se confirmó en el servidor | §7 |
| Reordenar la malla por su cuenta | Consistencia espacial |

## 6.2 Aislamiento de datos en la interfaz `[SEGURIDAD DE DATOS]`

El aislamiento del §2.2 tiene consecuencias visuales concretas, no solo de permisos:

| Pantalla | Qué muestra |
|---|---|
| Aviso de fatiga a todos los supervisores *(D2)* | `L4 · Puesto 3 — relevo sugerido · 62 min`. **Ninguna identidad**, ni al abrirlo |
| Detalle de solicitud en la L8 *(D1)* | Línea, puesto, tipo, fatiga, capacidades exigidas, perfil. **Nunca** nombre ni restricciones médicas del operario a relevar |
| Aviso de tránsito entrante | El destino **sí** ve el nombre: va a recibir a esa persona y debe confirmarla |
| Personal en Bolsón | El supervisor de L8 ve datos médicos **solo de su propio personal** |
| Panel del Coordinador | Ve las 10 líneas; las restricciones médicas se consultan bajo demanda, no se precargan *(D3)* |

> **Regla de diseño derivada:** el componente que renderiza una tarjeta de persona **no debe existir** en las pantallas de puesto ajeno. La protección se implementa por composición, no por ocultar campos: un campo oculto se enseña por error en el primer refactor.

## 6.3 Animación

| Transición | Duración |
|---|---|
| Cambio de estado | 150 ms |
| Aparición de modal | 200 ms |
| Banner offline | 250 ms |
| Pulso de esqueleto | 1200 ms |
| Pulso de fatiga crítica | 2000 ms, muy sutil |

Sin animaciones decorativas. La animación existe para explicar de dónde viene algo, nunca para adornar. Con movimiento reducido activado, todas se sustituyen por transición instantánea.

---

# 7 · Micro-copia

## 7.1 Micro-copia contextual de puesto — LITERAL DE §12.5

> **Esta tabla es intocable.** Está definida en la especificación funcional y **no se reinventa, no se reescribe, no se "mejora"**. Se transcribe.

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

## 7.2 Micro-copia añadida

Estados creados por decisiones posteriores a la especificación. **Marcada como añadida**, redactada en el mismo registro que la original.

| Situación | Mensaje | Origen |
|---|---|---|
| Rotativo descubierto | *"Sin ocupante — pendiente de cubrir"* | C11 |
| Puesto reservado | *"Reservado — [Nombre] viene en camino"* | §9.4 p3 |
| Tránsito demorado | *"Relevista demorado — supera el tiempo previsto"* | B11 |
| Vacante crítica en operación | *"Máquina sin operador — titular retirado"* | C15 |
| Sin restricciones médicas | *"Sin restricciones médicas registradas"* | §12.4 |
| Datos sin conexión | *"Datos de hace N min"* | D4 |
| Eficiencia sin registro reciente | *"Estimada desde el último registro — hace N min"* | C4 |

## 7.3 Registro de voz

Se escribe **en lenguaje de planta** *(§1.3)*:

| Se escribe así | No así |
|---|---|
| *"El Puesto 3 acaba de ser ocupado por Juan Pérez."* | *"Error 409: conflicto de recurso"* |
| *"María tiene restringido levantar carga y este puesto lo exige."* | *"Validación fallida: restricción médica"* |
| *"Ventana de arranque activa. Quedan 6 min."* | *"Operación no permitida en este momento"* |
| *"Llama al supervisor de la L2: tiene a Juan sin recibir."* | *"Existen operaciones pendientes"* |

Reglas: nombre propio siempre que se hable de una persona · número de puesto y línea siempre explícitos · **siempre el siguiente paso**, nunca solo el problema · sin jerga técnica, sin códigos, sin siglas internas.

---

# 8 · Trazabilidad

| Sección | Origen |
|---|---|
| 1 Principios | §1.3, §12.2, §12.3, §12.4 |
| 2.1 Tokens de estado | §5.3, §9.1, C11, B11, C15 |
| 2.1 Color médico | §7.2, §12.2 |
| 2.2 Tipografía ≥ 15 sp | §12.3 |
| 2.3 `space.touch` | §12.3 |
| 3.1 Tarjeta de puesto | §5.3, §9.1, §12.5, A4 |
| 3.2 Tarjeta de persona | §11.5, §12.2, B7 |
| 3.3 Modal de identidad | §12.2, D3 |
| 3.6 Banner offline | §12.1 |
| 3.7 Sello de frescura | D4 |
| 3.8 Cronómetro | §11.1 |
| 3.9 Estadística | §11.4, C4 |
| 3.11 Cuatro estados | §12.4 |
| 4.2 Zonas del pulgar | §12.3 |
| 4.5 Panel Bolsón | C7, C8 |
| 4.6 Datos maestros en teléfono | §2.1.10, F4, A1, A3 |
| 5.1 Zonas de toque 48/64 dp | §12.3, **A11** |
| 5.2 Contraste AAA | §12.3 |
| 5.3 Nunca solo color | §12.2 |
| 5.5 Doble toque | §12.4 |
| 6.2 Aislamiento visual | §2.2, D1, D2, D3 |
| 7.1 Micro-copia | **§12.5 literal** |
