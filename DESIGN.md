---
name: SmartAssign
description: Sistema de diseño oscuro y de alto contraste para gestión de personal de planta — construido para leerse de pie, en movimiento, con una mano y bajo luz industrial variable.
colors:
  accion-primaria: "#145DEB"
  bg-base: "#0E1116"
  bg-surface: "#171C23"
  bg-surface-raised: "#212832"
  bg-overlay: "rgba(0, 0, 0, 0.72)"
  border-subtle: "#2C3540"
  border-strong: "#617288"
  text-primary: "#F4F7FA"
  text-secondary: "#B4BFCC"
  text-disabled: "#6B7684"
  estado-libre: "#4A90D9"
  estado-ocupado: "#3FA76A"
  estado-critico: "#E5484D"
  estado-descubierto: "#D9822B"
  estado-fuera: "#5A6472"
  fatiga-normal: "#3FA76A"
  fatiga-sugerido: "#D9822B"
  fatiga-critico: "#E5484D"
  color-exito: "#3FA76A"
  color-alerta: "#D9822B"
  color-peligro: "#E5484D"
  color-medico: "#B5179E"
  color-medico-texto: "#F195E4"
  color-peligro-texto: "#F19C9E"
  color-alerta-texto: "#E4A668"
  color-transito: "#7B5CD6"
  color-offline-fg: "#E8C88A"
  color-offline-bg: "#2B2113"
typography:
  display:
    fontFamily: "Roboto (system default, not declared explicitly)"
    fontSize: "34sp"
    fontWeight: 700
  title:
    fontFamily: "Roboto"
    fontSize: "26sp"
    fontWeight: 700
  subtitle:
    fontFamily: "Roboto"
    fontSize: "20sp"
    fontWeight: 600
  body:
    fontFamily: "Roboto"
    fontSize: "18sp"
    fontWeight: 400
  body-strong:
    fontFamily: "Roboto"
    fontSize: "18sp"
    fontWeight: 600
  action:
    fontFamily: "Roboto"
    fontSize: "24sp"
    fontWeight: 600
  caption:
    fontFamily: "Roboto"
    fontSize: "16sp"
    fontWeight: 400
  label:
    fontFamily: "Roboto"
    fontSize: "15sp"
    fontWeight: 600
  mono:
    fontFamily: "Roboto Mono (system monospace)"
    fontSize: "18sp"
    fontWeight: 500
rounded:
  sm: "8dp"
  md: "12dp"
  lg: "20dp"
  pill: "999dp"
spacing:
  xs: "4dp"
  sm: "8dp"
  md: "16dp"
  lg: "24dp"
  xl: "32dp"
  touch: "8dp"
components:
  button-primary:
    backgroundColor: "{colors.accion-primaria}"
    textColor: "{colors.text-primary}"
    typography: "{typography.action}"
    height: "64dp"
  button-secondary:
    backgroundColor: "transparent"
    textColor: "{colors.accion-primaria}"
    typography: "{typography.action}"
    height: "56dp"
  card-puesto:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.md}"
    padding: "{spacing.md}"
  modal-confirmacion:
    backgroundColor: "{colors.bg-surface}"
    textColor: "{colors.text-primary}"
    rounded: "{rounded.lg}"
    padding: "{spacing.lg}"
---

# Design System: SmartAssign

## Overview

**Creative North Star: "Gestión de Personal"**

El nombre no es una metáfora decorativa — es deliberado, y así lo eligió el cliente. Encaja con el propio principio que gobierna el producto (`docs/01_PRD.md §1.3`): el sistema nunca dice ser otra cosa de lo que es. No hay una fachada de marca sobre la herramienta; la herramienta se llama por lo que hace.

Visualmente, eso se traduce en un sistema que se comporta como un panel de instrumentos de planta, no como una app de consumo: fondo oscuro fijo (nunca depende del tema del teléfono), lecturas de altísimo contraste calibradas contra el peor caso de lectura —protector rayado, brillo parcial, tubo fluorescente— y un vocabulario de color estrictamente semántico donde cada estado tiene su color, su forma y su ícono propios, nunca solo uno de los tres. Nada se comunica de forma ambigua porque la ambigüedad, en este contexto, tiene consecuencia física: una persona mal asignada a un puesto que su condición prohíbe.

La densidad es alta en información pero nunca en decoración: cada pantalla existe para que alguien de pie, caminando, con una mano, decida algo en segundos. No hay ilustración, no hay imaginería de marca, no hay motion de deleite — el presupuesto visual entero se gasta en legibilidad y en la honestidad de cada estado.

**Key Characteristics:**
- Oscuro siempre, sin modo claro y sin seguir el tema del sistema — es una decisión de legibilidad bajo luz industrial variable, no una preferencia estética (`ui/theme/Theme.kt`).
- Cuatro tratamientos de pantalla que nunca se confunden entre sí: cargando, vacío legítimo, fuera de operación, error — un `when` exhaustivo en código impide que se agregue un quinto sin darle su propio tratamiento.
- Ningún estado se comunica solo con color: siempre texto + forma + ícono.
- Contraste AAA (7:1 texto normal, 4.5:1 texto ≥ 24sp, 3:1 elementos de interfaz) como piso, no como aspiración.
- Zonas de toque generosas (48dp piso, 64dp en la acción primaria) para operar de pie, con una mano, sin mirar fijamente.
- El dato nunca se oculta cuando envejece: se degrada visiblemente (60% de opacidad + marca de agua diagonal) en vez de desaparecer o de mentir sobre su vigencia.

## Colors

Una paleta casi monocroma de fondo — negro de planta y sus superficies — sobre la que un vocabulario de color estrictamente semántico marca estado, nunca decoración.

### Primary
- **Azul de Señal** (`#145DEB`): la única acción primaria de cualquier pantalla — el botón que más se toca y el que menos puede fallar. Su profundidad está calibrada al límite (`00_DECISIONES.md §A15`): es el punto exacto que sostiene 5.15:1 de contraste en el rótulo del botón sin que el botón mismo se pierda contra el fondo (3.42:1).

### Neutral
- **Negro de Planta** (`#0E1116`): fondo de toda la app. Fijo — no varía con el tema del sistema ni con la hora del día.
- **Superficie Oscura** (`#171C23`): tarjetas, hojas, paneles.
- **Superficie Elevada** (`#212832`): tarjeta seleccionada, modal.
- **Velo de Modal** (`rgba(0,0,0,0.72)`): fondo tras un diálogo.
- **Borde Sutil** (`#2C3540`): separadores.
- **Borde Fuerte** (`#617288`): borde de componente interactivo. Su valor está fijado por el mínimo de 3:1 para elementos de interfaz, no por gusto.
- **Texto Principal** (`#F4F7FA`): texto de lectura primaria.
- **Texto Secundario** (`#B4BFCC`): microcopia, texto de apoyo.
- **Texto Inactivo** (`#6B7684`): solo para texto no accionable.

### Estados de puesto
- **Azul de Puesto Libre** (`#4A90D9`): borde discontinuo, contorno vacío.
- **Verde de Ocupado** (`#3FA76A`): borde sólido, silueta llena.
- **Rojo de Vacante Crítica** (`#E5484D`): borde sólido grueso + franja lateral de 8dp, triángulo de alerta.
- **Ámbar de Rotativo Descubierto** (`#D9822B`): borde discontinuo grueso, contorno con punto.
- **Gris de Fuera de Operación** (`#5A6472`): sin borde, superficie hundida al 55% de opacidad, diagonal tachada.

**La Regla del Tercer Estado.** "Fuera de operación" nunca comparte tratamiento visual con "libre" ni con "ocupado" — es el único estado con superficie hundida y opacidad reducida, para que se lea como "esto no existe hoy" antes incluso de leer el texto.

### Niveles de fatiga
- **Verde Normal** (`#3FA76A`): barra fina.
- **Ámbar Sugerido** (`#D9822B`): barra media + ícono de reloj.
- **Rojo Crítico** (`#E5484D`): barra gruesa + ícono de alerta + pulso lento (2s, alpha 0.5→1).

**La Regla del Umbral Propio.** La barra de fatiga nunca representa minutos absolutos — siempre el porcentaje sobre el umbral propio de ese puesto. Dos puestos con el mismo tiempo transcurrido pueden mostrar barras muy distintas entre sí, y eso es correcto, no un error de cálculo.

### Semánticos de sistema
- **Verde de Éxito** (`#3FA76A`)
- **Ámbar de Alerta** (`#D9822B`)
- **Rojo de Peligro** (`#E5484D`)
- **Magenta Restringido** (`#B5179E`): exclusivo de restricciones médicas, como relleno.
- **Magenta Restringido Legible** (`#F195E4`): el mismo magenta como texto, icono y borde.
- **Rojo de Peligro Legible** (`#F19C9E`) y **Ámbar de Alerta Legible** (`#E4A668`): las variantes de texto de sus respectivos colores de identidad.
- **Violeta de Tránsito** (`#7B5CD6`): persona en tránsito, puesto reservado.
- **Ámbar Claro sobre Marrón Oscuro** (`#E8C88A` sobre `#2B2113`): banner de sin conexión — el ámbar pinta texto, icono **y borde**, porque es el borde y no el fondo quien define el bloque.

**La Regla del Magenta Exclusivo.** El color de restricción médica nunca se reutiliza para ninguna otra alerta. Es la única información de la aplicación cuyo malentendido produce daño físico real, y necesita ser inconfundible incluso para alguien que ya asoció los demás colores a otras cosas.

**La Regla de las Dos Mitades.** Un color que sirve a la vez de relleno y de texto tiene dos umbrales de contraste y no puede cumplir los dos con un solo valor. No se resuelve moviendo el token hasta que "se lea bien" —eso lo vuelve pastel y le quita la identidad—, sino partiéndolo: la identidad se queda para el relleno y una variante más clara cubre el texto. Precedente: A15 (azul de acción) y A16 (médico, peligro, alerta, banner sin conexión).

## Typography

**Display/Body Font:** Roboto (fuente del sistema Android; no se declara `fontFamily` explícito para no depender de una descarga externa).
**Mono Font:** Roboto Mono (monospace del sistema, resuelto localmente).

**Character:** utilitaria y sin personalidad decorativa a propósito. La app no compite por atención — compite por leerse de un vistazo, de pie, a distancia de brazo, con la pantalla a brillo parcial.

### Hierarchy
- **Display** (700, 34sp): cronómetro de paro, cifra de eficiencia — los números que se leen desde más lejos.
- **Title** (700, 26sp): título de pantalla, nombre de puesto.
- **Subtitle** (600, 20sp): nombre de persona dentro de una tarjeta.
- **Body** (400, 18sp): texto general. No los 14–16sp habituales de una app de escritorio — se lee de pie, en movimiento.
- **Body Strong** (600, 18sp): el dato que se lee de un vistazo dentro de un bloque de cuerpo.
- **Action** (600, 24sp): rótulo de botón. Token propio, no comparte tamaño con ningún otro texto de la escala.
- **Caption** (400, 16sp): microcopia contextual.
- **Label** (600, 15sp): etiquetas de grupo/sección.
- **Mono** (500, 18sp, monospace): número de ficha, identificador de puesto.

**La Regla del Piso de 15.** Ningún tamaño de texto baja de 15sp en ninguna pantalla de la aplicación. Si un contenido no cabe a 15sp, el problema es el contenido, no el tamaño.

**La Regla del Rótulo Grande.** El texto de un botón nunca hereda el tamaño de una etiqueta pequeña — vive en su propio token (`type.action`, 24sp) porque es, por definición, el control que más se toca y el que menos puede fallar. Antes de esto compartía tamaño con `type.caption` (16sp) y era, sin querer, de las letras más pequeñas de la app en el control más importante.

## Layout

Escala base de **4dp**. La jerarquía de pantalla sigue el "mapa del pulgar": la información vive arriba, la acción frecuente vive en el tercio inferior, al alcance del pulgar de quien sostiene el teléfono con una mano.

Las listas de puestos (mal llamadas "malla" en el nombre del componente, pero implementadas como lista, no como grilla) se agrupan en bloques fijos: **puestos fijos primero, rotativos después, fuera de operación al final y colapsado por defecto** — el orden dentro de cada grupo lo decide siempre el servidor, nunca el cliente. La acción primaria de cada pantalla de línea (escanear gafete) vive en un FAB flotante inferior-derecho, nunca compitiendo por espacio con el contenido de lectura.

Padding estándar de contenido: `spacing.md` (16dp). Separación entre bloques de sección: `spacing.lg` (24dp) o `spacing.xl` (32dp) para el margen superior de una sección nueva. Dos zonas de toque nunca quedan a menos de `spacing.touch` (8dp) una de otra — se toca caminando y mirando la línea, no la pantalla.

## Elevation & Depth

El sistema es mayormente **plano**: las tarjetas (`TarjetaDePuesto`) no llevan sombra, solo un bloque de color de superficie (`bg-surface`) con esquinas redondeadas — la profundidad se transmite por **capas tonales** (`bg-base` → `bg-surface` → `bg-surface-raised`), no por `box-shadow`. La única elevación real observada en código es el modal de confirmación de identidad, con `tonalElevation` en el nivel más alto de la escala — es, a propósito, la única superficie que debe sentirse por encima de todo lo demás porque interrumpe una decisión de seguridad.

La escala de elevación está declarada completa en `ui/theme/Dimens.kt` (`Elevation.nivel0/1/2/3` = 0/2/6/12dp, mapeados a base/tarjeta/hoja/modal) pero solo el nivel 3 (modal) está aplicado hoy; los niveles 1 y 2 son la reserva declarada para hojas y tarjetas elevadas cuando ese tratamiento se implemente.

**La Regla de la Opacidad como Profundidad.** Donde la app necesita decir "esto no es el estado normal", no usa sombra — usa opacidad reducida: 55% para un puesto fuera de operación, 60% para contenido con datos desincronizados. La reducción de opacidad es el lenguaje de "esto está degradado", reservado exclusivamente para ese significado.

## Shapes

Escala de radios: `sm` (8dp, franja fina de estado / detalles pequeños), `md` (12dp, tarjetas y contenedores estándar), `lg` (20dp, hojas y el modal de confirmación), `pill` (999dp, barra de fatiga y la forma por defecto de los botones Material3, sin sobreescribir).

La franja lateral de color en `TarjetaDePuesto` no es puramente decorativa: su **ancho** codifica urgencia además del color — 4dp en estados normales, 6dp en "rotativo descubierto", 8dp en "vacante crítica". Es una señal de forma independiente del color, consistente con el principio de nunca comunicar solo con un canal.

## Components

### Buttons
- **Shape:** forma por defecto de Material3 (redondeo total / pill), sin sobreescribir en código.
- **Primary:** fondo `Azul de Señal`, rótulo `Texto Principal` en `type.action` (24sp/600), altura **64dp** (`TouchTarget.accionPrimaria`). Uno solo por pantalla — la acción que más se usa y la que menos puede fallar ("Entrar", "Abrir cámara", "CONFIRMAR ASIGNACIÓN").
- **Secondary (Outlined):** fondo transparente, rótulo en `Azul de Señal` (color de contenido por defecto de Material3 sobre el esquema del tema), altura **56dp** (`TouchTarget.accionSecundaria`) — usado para acciones reversibles como "Cancelar", nunca preseleccionado.
- **Destructivo:** especificado en `docs/03_UIUX_BRIEF.md §3.4` (color de peligro + ícono + confirmación) pero **no implementado todavía** en el código leído — hueco conocido, no inventado aquí.

### Inputs / Fields
- **Style:** `OutlinedTextField` de Material3, sin personalización más allá del esquema de color global — sin trazo, radio ni estado de foco propios todavía. Campo de contraseña con `PasswordVisualTransformation`.
- **Error / Disabled:** no observado como estado propio del campo; los errores de formulario se muestran como texto aparte en `color.peligro`, no como borde de error del campo.

### Cards / Containers — Tarjeta de Puesto (componente central, 03 §3.1)
El componente que más se repite en la app — de 8 a 20 veces por pantalla. Anatomía fija:
- **Corner Style:** `rounded.md` (12dp).
- **Background:** `Superficie Oscura`, con una franja lateral de color de estado (ver Shapes) — salvo en "fuera de operación", que pierde la franja y baja a 55% de opacidad.
- **Contenido interno** (`spacing.md` de padding): código de puesto en `type.mono` + tipo (Fijo/Rotativo) en `type.label`; nombre del ocupante en `type.subtitle` con ícono de doble turno si aplica; ficha + categoría en `type.body`, con contador de restricciones médicas en `Magenta Restringido` si aplica; barra de fatiga **solo cuando hay un dato real** (nunca una barra inventada); microcopia contextual siempre presente en `type.caption`, nunca omitida aunque no haya nada urgente que decir.
- **Shadow Strategy:** ninguna — ver Elevation & Depth.

### Modal — Confirmación de Identidad (componente de seguridad, 03 §3.3)
El único punto donde un escaneo de gafete se convierte en una asignación real.
- **Corner Style:** `rounded.lg` (20dp). **Background:** `Superficie Oscura`. **Elevation:** nivel 3 (12dp).
- No responde a back ni a toque fuera del diálogo (`dismissOnBackPress = false`, `dismissOnClickOutside = false`) — la salida es siempre deliberada, nunca accidental.
- El bloque de restricciones médicas **nunca se colapsa ni se abrevia**: lleva borde propio de 2dp en `Magenta Restringido`, y afirma explícitamente la ausencia de restricciones en vez de omitir el bloque cuando no hay ninguna.
- Único botón de salida (`Cancelar`, 56dp) presente en todo estado — cargando, error o listo. Confirmar es un botón primario de 64dp, ancho completo.

### Screen States — los cuatro estados honestos (patrón de todo el sistema, 03 §3.11)
Cada pantalla que depende de datos remotos pasa por un `when` exhaustivo con cinco ramas, no cuatro genéricas:
- **Cargando:** esqueleto con la forma real del contenido (nunca un spinner genérico), pulsando entre 40% y 100% de opacidad cada 1.2s.
- **Listo:** el contenido real.
- **Vacío legítimo:** ícono + mensaje + siguiente paso explícito, centrado.
- **Fuera de operación:** superficie hundida (`Negro de Planta`, 55% opacidad) + ícono diagonal-tachado + motivo.
- **Error:** ícono de alerta en `Rojo de Peligro` + causa concreta + acción sugerida concreta — nunca un mensaje genérico ni un código.

### Freshness & Degradation (patrón de honestidad de datos, 00 §D4, 03 §3.7)
- **Sello de Frescura:** línea de `type.caption` bajo la cabecera — "Datos de hace N min" — que pasa de `Texto Secundario` a `Ámbar de Alerta` al superar el umbral (5 min por defecto).
- **Degradación de Contenido:** el contenido nunca se oculta ni se reemplaza al envejecer — se muestra al 60% de opacidad con una marca de agua diagonal rotada ("SIN SINCRONIZAR") sobre él. Un aviso se ignora; un contenido que se ve distinto no.

### Offline Banner
Barra de ancho completo en `Ámbar de Alerta`, ícono de nube tachada + texto en `type.body.strong`, con la copia literal de la especificación: *"Pendiente de sincronización — no mover al personal hasta recuperar la red."*

### Stop Timer Bar — Cronómetro de Paro
Barra persistente de ancho completo, por encima de la navegación, visible en cualquier pantalla mientras un paro está abierto. Fondo `Superficie Elevada`, borde de 1dp en `Rojo de Peligro`, duración en `type.display` con dígitos tabulares (`HH:MM:SS`, nunca negativa). Solo desaparece al reanudar producción explícitamente — no hay estado intermedio ni animación de salida.

### Navigation
Un solo FAB por pantalla para la acción primaria (por ejemplo, escanear gafete), sin personalizar más allá del estilo por defecto de Material3. No se observó una barra de navegación inferior en el código leído: las pantallas son mayormente flujos de un solo propósito, acotados por rol.

## Do's and Don'ts

### Do:
- **Do** mantener la app oscura siempre — sin esquema claro, sin seguir `isSystemInDarkTheme()`. `Theme.kt` es explícito: una rama clara fue exactamente el bug que dejó el contraste en 1.10:1.
- **Do** envolver todo contenido de pantalla en un `Surface` (o heredar el `Surface` raíz de `SmartAssignTheme`) para que `LocalContentColor` se resuelva — un texto sin color explícito cae al negro por defecto de Material3 si nadie fija ese contenedor.
- **Do** mantener la barra de fatiga relativa al umbral propio de cada puesto, nunca a minutos absolutos.
- **Do** acompañar todo estado con texto + forma/ícono, nunca solo color.
- **Do** mantener el mensaje de rechazo o error nombrando la causa concreta y el siguiente paso concreto — nunca un texto genérico ni un código.
- **Do** degradar visiblemente el contenido desincronizado (opacidad + marca de agua) en vez de ocultarlo o mostrarlo como si estuviera vivo.

### Don't:
- **Don't** introducir un `lightColorScheme` ni seguir el tema del sistema — esa regresión exacta ya ocurrió una vez y hoy está explícitamente documentada como advertencia en el código.
- **Don't** reemplazar el esqueleto de carga con forma real por un spinner genérico.
- **Don't** inventar un lenguaje de sombras pesadas — la profundidad viene de capas tonales de superficie y de opacidad, no de `box-shadow`; solo el modal de confirmación lleva elevación real.
- **Don't** bajar ningún tamaño de texto de 15sp en ninguna pantalla.
- **Don't** reutilizar el `Magenta Restringido` (`#B5179E`) para ninguna otra alerta — es exclusivo de restricciones médicas, la única información cuyo malentendido causa daño físico real.
