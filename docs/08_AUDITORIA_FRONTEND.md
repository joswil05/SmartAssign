# SmartAssign — Auditoría de frontend

**Informe de defectos y plan de corrección del cliente Android.**
Versión 1.1 · 2026-08-13 · Auditado sobre `83bb958` (E14.6, 87/95 UTs)

> **Estado: E15.1–E15.7 aplicadas.** Este documento se conserva como el registro de por qué se cambió cada cosa. Lo pendiente es E16 (§6, coherencia del sistema de diseño) y la ejecución de las pruebas instrumentadas nuevas, que no se pudieron correr por no haber emulador disponible en la sesión de la auditoría.

> **Qué es este documento.** Una auditoría técnica del cliente Android contra tres autoridades, en este orden: `03_UIUX_BRIEF.md` (el sistema de diseño propio), `00_DECISIONES.md` (A11, A15, D4, B12) y las convenciones de Material Design 3 / Android. **No es una crítica de diseño ni propone rediseñar nada**: cada hallazgo es una desviación medible respecto de una regla que este proyecto ya escribió.
>
> **Regla R2 respetada.** Donde la corrección exige un dato o criterio que ninguna fuente cierra, el hallazgo se marca `⚠ REQUIERE DECISIÓN` y **no se propone un valor inventado**. Hay exactamente uno de esos: §D-1.
>
> **Método.** Lectura completa de los 24 ficheros de `ui/`, más manifiesto, `themes.xml`, `build.gradle.kts` y la suite de 23 pruebas instrumentadas + 30 unitarias. Todos los contrastes están **calculados**, no estimados: fórmula de luminancia relativa de WCAG 2.1, la misma que ya usan `DesignTokensTest` y `TemaAplicadoContrasteTest`.

---

# 1 · Puntuación

| # | Dimensión | Puntuación | Hallazgo principal |
|---|---|---|---|
| 1 | Accesibilidad | **2 / 4** | 5 pares de color incumplen el AAA que exige `03 §5.2`, incluido el de restricciones médicas a 2.92:1 |
| 2 | Rendimiento | **3 / 4** | Sano. Una animación infinita por tarjeta crítica mantiene el bucle de fotogramas vivo |
| 3 | Apariencia y tematización | **3 / 4** | Tokens excelentes, pero el banner de sin conexión ignora los suyos y hay medidas sueltas fuera de escala |
| 4 | Conformidad con la plataforma | **2 / 4** | `enableEdgeToEdge()` activo **sin una sola referencia a insets** en toda la app |
| 5 | Adaptabilidad | **1 / 4** | Ninguna pantalla con campos hace scroll ni aparta el teclado |
| | **Total** | **11 / 20** | **Aceptable — trabajo significativo pendiente** |

> **Cómo leer este 11/20 sin malinterpretarlo.** La puntuación mide desviaciones concretas, no la calidad de la ingeniería. Este código tiene una disciplina de pruebas muy por encima de lo habitual: `TemaAplicadoContrasteTest` verifica el color **realmente rasterizado** y no solo el token, y `TarjetaDePuestoOperableEnGrisesTest` prueba la arquitectura de tres canales sin que ninguna aserción dependa del color. Eso es notable y hay que conservarlo.
>
> Lo que la puntuación revela es **dónde no llegó ese mismo rigor**: la verificación de contraste cubre los tokens de texto principales y el botón primario, pero nunca se extendió a los colores semánticos usados como texto. Y la conformidad con Android (insets, teclado, escala de fuente) no tiene ninguna prueba todavía. Son huecos de cobertura, no de criterio.

---

# 2 · Veredicto de conformidad con Android

**No pasa.** No por parecer una web portada —no lo parece en absoluto: usa componentes Material 3 nativos, iconografía Material coherente, `NavHost` correcto y no hay un solo control con forma de iOS ni afordancia de hover— sino por **una omisión concreta y verificable**:

```
MainActivity.kt:45   enableEdgeToEdge()
grep -rn "WindowInsets|safeDrawing|systemBars|imePadding|Scaffold"  →  0 resultados
```

`enableEdgeToEdge()` le dice al sistema que la app dibujará **por debajo de la barra de estado y de la barra de navegación**, y asume el compromiso de aplicar los insets por su cuenta. La app no lo hace en ningún punto. Consecuencias reales, no teóricas:

- El **banner de sin sincronizar** —el aviso que `§12.1` define como *"permanente e inequívoco"*— es el primer elemento de la `Column` raíz y queda **parcialmente debajo del reloj y la batería**. El aviso más crítico de la aplicación es justo el que la barra de estado tapa.
- El **botón de acción primaria** de cada pantalla queda bajo la barra de navegación por gestos.
- El **FAB de escanear gafete** (`Alignment.BottomEnd` + `padding(24dp)`) queda dentro de la zona del gesto de retorno del sistema en teléfonos con navegación por gestos.

El resto de la conformidad es correcta: Back del sistema respetado y no secuestrado, `popUpTo` bien planteado para que "atrás" no vuelva a pedir credenciales, un solo FAB por pantalla, sin navegación inventada.

---

# 3 · Resumen ejecutivo

- **19 hallazgos**: 3 `P0` · 8 `P1` · 6 `P2` · 2 `P3`
- **1 hallazgo requiere decisión humana** (`⚠ D-1`), porque la corrección "obvia" —usar los tokens que el propio brief define— tampoco cumple la norma del propio brief.
- **Ningún hallazgo es un rediseño.** Todos son "el código no hace lo que el documento dice" o "Android exige algo que no se está haciendo".

**Los cinco que más importan:**

1. **`P0-1`** — Cero manejo de insets con edge-to-edge activo. El aviso de sin conexión queda bajo la barra de estado.
2. **`P0-2`** — El color de restricciones médicas, usado como texto, da **2.92:1**. Exige 7:1 y no alcanza ni el 4.5:1 de AA. Es la única información de la app cuyo malentendido produce daño físico, y es la peor de toda la paleta.
3. **`P0-3`** — Login y PIN no hacen scroll ni apartan el teclado: con el teclado abierto, el botón queda inalcanzable en pantallas cortas.
4. **`P1-1`** — El indicador médico se anuncia a TalkBack como el número suelto **"2"**, sin decir de qué.
5. **`P1-2`** — El banner de sin conexión da **2.72:1** y no usa ninguno de los dos tokens que `03 §2.1` definió para él.

---

# 4 · Hallazgos P0 — bloqueantes

## P0-1 · Edge-to-edge activo sin ningún manejo de insets

| | |
|---|---|
| **Ubicación** | `MainActivity.kt:45` (`enableEdgeToEdge()`), `MainActivity.kt:76` (`Column` raíz sin insets) |
| **Categoría** | Conformidad / Adaptabilidad |
| **Regla** | Material 3 — *"Edge-to-edge with window insets: apply the status bar, navigation bar, display cutout, and IME insets"* |

**Impacto.** El `BannerSinSincronizar` y el `CronometroDeParo` son barras superiores fijas y quedan bajo la barra de estado; los botones primarios inferiores quedan bajo la barra de navegación. `§12.1` exige que el aviso de sin conexión sea *"permanente e inequívoco"* — uno tapado a medias por el reloj del sistema no lo es.

**Corrección propuesta.** Aplicar el inset seguro una sola vez, en la `Column` raíz, no pantalla por pantalla:

```kotlin
// MainActivity.kt
import androidx.compose.foundation.layout.safeDrawingPadding

Column(modifier = Modifier.fillMaxSize().safeDrawingPadding()) {
    BannerSinSincronizar(visible = mostrarBanner)
    CronometroDeParo(paro = paroActivo)
    NavHost(...)
}
```

> **Por qué `safeDrawingPadding()` en la raíz y no un `Scaffold` por pantalla:** el banner y el cronómetro viven deliberadamente **fuera** del `NavHost` para sobrevivir a la navegación (E11.3, E13.4). Un `Scaffold` por pantalla dejaría esas dos barras —las dos únicas que `03 §3.8` exige que sean persistentes— sin protección. La raíz es el único punto que las cubre a las dos.

---

## P0-2 · El color de restricciones médicas incumple incluso AA cuando se usa como texto

| | |
|---|---|
| **Ubicación** | `TarjetaDePuesto.kt:112` · `ModalConfirmacionIdentidad.kt:168,172` |
| **Categoría** | Accesibilidad |
| **Regla** | `03 §5.2` — 7:1 texto normal · 3:1 elementos de interfaz |

**Medición.** `ColorMedico` = `#B5179E`.

| Uso real | Contraste | Exige | |
|---|---|---|---|
| Texto del contador médico sobre `BgSurface` | **2.92:1** | 7:1 | ✗ |
| Título "⚕ RESTRICCIONES MÉDICAS ACTIVAS" sobre `BgSurface` | **2.92:1** | 7:1 | ✗ |
| Borde de 2 dp del bloque médico sobre `BgSurface` | **2.92:1** | 3:1 | ✗ |
| Tinte del icono sobre `BgSurface` | **2.92:1** | 3:1 | ✗ |

**Impacto.** `03 §2.1` justifica que este color exista precisamente porque *"es la única información de la app cuyo malentendido produce daño físico a una persona"*. Hoy es el color menos legible de toda la paleta, en las dos pantallas donde aparece, y bajo las condiciones de `§12.3` (mica rayada, brillo parcial, tubo fluorescente) para las que el AAA se eligió expresamente.

**Corrección propuesta — dos tokens, no uno.** Es exactamente la estructura con la que se resolvió **A15**: el problema no se arregla moviendo un solo valor, porque el token tiene dos trabajos con dos umbrales distintos.

```kotlin
// Color.kt
val ColorMedico = Color(0xFFB5179E)        // identidad — franjas y rellenos amplios
val ColorMedicoTexto = Color(0xFFEE7DDE)   // NUEVO — texto, iconos y bordes sobre superficie
```

`#EE7DDE` conserva el tono (309°) y la saturación (77%) del magenta original, solo más claro. Rinde **7.77:1 sobre `BgBase`** y **7.03:1 sobre `BgSurface`** — cumple AAA en las dos superficies con margen, y sigue siendo inconfundiblemente el magenta médico, sin acercarse a ningún otro color de la paleta.

---

## P0-3 · Login y PIN quedan inutilizables con el teclado abierto

| | |
|---|---|
| **Ubicación** | `LoginScreen.kt:88` · `PinScreen.kt:38` |
| **Categoría** | Adaptabilidad |
| **Regla** | Material 3 — insets de IME · `03 §2.2` — *"el escalado tipográfico se respeta hasta el 130% sin que se rompa ningún layout"* |

**Impacto.** Las dos pantallas son una `Column` con `fillMaxSize()` + `Arrangement.Center`, **sin `verticalScroll` y sin `imePadding()`**. Al abrirse el teclado:

- el contenido no se desplaza y el botón primario queda debajo del teclado, sin forma de alcanzarlo;
- en `LoginScreen`, que además puede mostrar el banner de actualización disponible (E14.6), dos campos, dos líneas de error y el botón, el contenido **ya no cabe** en una pantalla corta ni siquiera sin teclado;
- a 130% de escala de fuente —el nivel que `03 §2.2` promete soportar— el desbordamiento es seguro, y sin scroll no hay recuperación.

**Corrección propuesta.**

```kotlin
// LoginScreen.kt y PinScreen.kt
Column(
    modifier = Modifier
        .fillMaxSize()
        .verticalScroll(rememberScrollState())
        .imePadding()
        .testTag("pantalla-login")
        .padding(Spacing.xl),
    verticalArrangement = Arrangement.Center
)
```

> **Nota:** con `verticalScroll`, `Arrangement.Center` deja de centrar cuando el contenido desborda y pasa a comportarse como alineación superior — que es justo lo que se quiere. No hay que elegir entre las dos.

---

# 5 · Hallazgos P1 — corregir antes de liberar

## P1-1 · El indicador médico se anuncia a TalkBack como un número suelto

**Ubicación:** `TarjetaDePuesto.kt:111-113`

```kotlin
Icon(Icons.Filled.LocalHospital, contentDescription = null, tint = ColorMedico)
Text(" ${puesto.indicadorMedico}", style = TypeBody, color = ColorMedico)
```

Con `contentDescription = null` el icono es invisible para el lector de pantalla, así que TalkBack lee literalmente **"2"**, sin ninguna indicación de qué son esos dos. Es el mismo dato que `§7.2` protege con una regla dura.

**Corrección:** dar semántica al par completo y eliminar el nodo suelto.

```kotlin
Row(
    verticalAlignment = Alignment.CenterVertically,
    modifier = Modifier.semantics(mergeDescendants = true) {
        contentDescription = "${puesto.indicadorMedico} restricciones médicas activas"
    }
) { ... }
```

## P1-2 · El banner de sin conexión ignora sus propios tokens y da 2.72:1

**Ubicación:** `BannerSinSincronizar.kt:39,46` — ver **`⚠ D-1`**, que es la decisión que este hallazgo requiere.

`03 §2.1` define `color.offline` como `#8A6D3B` sobre `#2B2113`. El código usa `ColorAlerta` (`#D9822B`) de fondo y hereda `TextPrimary` del tema: **2.72:1**, por debajo incluso de AA. Los tokens `ColorOfflineFg` y `ColorOfflineBg` están declarados en `Color.kt` y **no se usan en ningún sitio**.

## P1-3 · El texto de error incumple AAA en las cuatro pantallas que lo usan

**Ubicación:** `LoginScreen.kt:122` · `PinScreen.kt:66` · `AltaDispositivoScreen.kt:100` · `FlujoAsignacion.kt` (icono)

`ColorPeligro` `#E5484D` como texto: **4.83:1 sobre `BgBase`**, **4.37:1 sobre `BgSurface`**. Exige 7:1. Cumple AA pero no el AAA que `§12.3` impone por las condiciones de planta — y el mensaje de rechazo es, por `§1.3`, funcionalidad de primera clase, no un caso de error.

**Corrección:** mismo patrón de dos tokens que P0-2.

```kotlin
val ColorPeligro = Color(0xFFE5484D)       // relleno, franja, borde, icono
val ColorPeligroTexto = Color(0xFFEE8A8D)  // NUEVO — 7.79:1 base / 7.06:1 surface
```

## P1-4 · El sello de frescura en alerta incumple AAA

**Ubicación:** `SelloDeFrescura.kt:70`

`ColorAlerta` `#D9822B` como texto: **6.46:1 sobre `BgBase`**, **5.85:1 sobre `BgSurface`**. Exige 7:1. Se queda corto por poco, pero es el indicador que `D4` usa para decir que un dato **ya no es de fiar**.

**Corrección:** `ColorAlertaTexto = Color(0xFFDF964D)` — 7.75:1 / 7.02:1, mismo tono 30°.

## P1-5 · El borde de los campos de texto incumple el mínimo de elementos de interfaz

**Ubicación:** `Theme.kt:47` (`outline = BorderStrong`), aplicado por Material 3 a todo `OutlinedTextField`

`BorderStrong` `#455161`: **2.34:1 sobre `BgBase`**, **2.12:1 sobre `BgSurface`**. `03 §5.2` exige 3:1 a elementos de interfaz, y WCAG 1.4.11 lo exige igual. El borde **es** la única afordancia visual del campo: si no se distingue, el campo no se ve.

**Corrección:** subir el token en el sitio (solo se usa como borde, no como texto, así que no necesita desdoblarse): `BorderStrong = Color(0xFF59687D)` — 3.33:1 / 3.02:1, mismo tono 214°.

## P1-6 · Dos botones por debajo del piso de 48 dp

**Ubicación:** `AltaDispositivoScreen.kt:106` ("Reintentar") · `EscaneoQrScreen.kt:163` ("Permitir cámara")

Ninguno declara altura, así que usan el defecto de Material 3: **40 dp**, por debajo del piso absoluto de `A11`.

> **Detalle que conviene registrar:** el docstring de `MallaLineaZonasDeToqueTest` afirma que *"todo el resto de botones de la app (`LoginScreen`, `PinScreen`, `ModalConfirmacionIdentidad`, `AltaDispositivoScreen`, `EscaneoGafeteScreen`) ya aplicaba `TouchTarget` explícitamente"*. Es cierto para el botón **primario** de cada una, pero estos dos secundarios quedaron fuera. La prueba de E14.5 no los cubre porque solo compone `MallaLineaScreen`.

**Corrección:** `.heightIn(min = TouchTarget.accionTerciaria)` en ambos, y una prueba que barra **todas** las pantallas, no solo la malla.

## P1-7 · Las alturas fijas rompen la promesa de 130% de escala de fuente

**Ubicación:** los 8 botones que usan `.height(TouchTarget.X)`

`Modifier.height()` fija una altura **exacta**. Con `TypeAction` a 24 sp dentro de un botón de 64 dp, al 130% de escala el rótulo pasa a ~31 sp y el texto se recorta, porque la altura no puede crecer. `03 §2.2` promete explícitamente que *"el escalado tipográfico del sistema se respeta hasta el 130% sin que se rompa ningún layout"*.

**Corrección:** sustituir `.height(x)` por `.heightIn(min = x)` en los 8 casos. El piso de toque se conserva intacto —que es lo que `A11` exige— y el botón puede crecer cuando el texto lo necesita.

## P1-8 · Ninguna animación respeta "Quitar animaciones" del sistema

**Ubicación:** `PantallaConEstado.kt:75` (pulso de esqueleto) · `TarjetaDePuesto.kt:153` (pulso de fatiga crítica)

Dos `rememberInfiniteTransition` que corren siempre. Android expone la preferencia de accesibilidad *Quitar animaciones*; ninguna de las dos la consulta. Material 3 lo pide expresamente, y aquí importa más de lo normal: el pulso de fatiga crítica es un canal de información, así que no basta con apagarlo — al desactivarse debe quedar el color pleno (alfa 1), nunca a media opacidad congelada.

---

# 6 · Hallazgos P2 y P3

| ID | Sev | Hallazgo | Ubicación |
|---|---|---|---|
| **P2-1** | P2 | `VistaVerificando` es un `CircularProgressIndicator` **solo**, sin una línea de texto — `§12.4` prohíbe el círculo girando desnudo, y `PantallaConEstado` fue construido justo para evitarlo | `AltaDispositivoScreen.kt:79-87` |
| **P2-2** | P2 | La marca de agua "SIN SINCRONIZAR" es un `Text` real: TalkBack la lee intercalada con el contenido degradado. Es decoración visual, debe ser `clearAndSetSemantics {}` | `ContenidoConDegradacion.kt:54` |
| **P2-3** | P2 | El `semantics { contentDescription }` del banner se aplica sin `mergeDescendants`, así que convive con el texto en vez de sustituirlo — TalkBack anuncia el aviso dos veces | `BannerSinSincronizar.kt:41` |
| **P2-4** | P2 | Medidas fuera de la escala de 4 dp sembradas por el código: `96.dp`, `120.dp`, `80.dp` (esqueletos), `14.dp`/`16.dp` (iconos), `2.dp` (borde médico). `03 §2.3` fija escala base 4 dp y no existe token de tamaño de icono | 5 ficheros |
| **P2-5** | P2 | `Elevation.nivel1` y `nivel2` declarados y nunca usados; `TarjetaDePuesto` es plana pese a que `§2.3` mapea `elevation.1` a "tarjeta" | `Dimens.kt:39-40` |
| **P2-6** | P2 | Cada tarjeta en fatiga crítica instancia su propia animación infinita. Con 20 puestos críticos son 20 bucles de fotograma permanentes en un teléfono de planta | `TarjetaDePuesto.kt:153` |
| **P3-1** | P3 | `TextDisabled` (`#6B7684`, 4.10:1) está declarado pero no se usa en ninguna pantalla. WCAG exime los controles deshabilitados, así que **hoy no es un defecto** — pero lo será en cuanto se use, y `03 §2.1` no lo advierte | `Color.kt:23` |
| **P3-2** | P3 | `ColorTransito` y `ColorExito` declarados y sin usar: las pantallas de tránsito y de confirmación aún no existen. No es defecto, es superficie por construir — se anota para que no se confunda con código muerto | `Color.kt:57,61` |

---

# 7 · Patrones sistémicos

Tres causas raíz explican 14 de los 19 hallazgos. Vale más atacarlas que ir hallazgo por hallazgo.

### 7.1 · La verificación de contraste cubre los tokens de texto, no los semánticos

`DesignTokensTest` comprueba `TextPrimary` y `TextSecondary`. `TemaAplicadoContrasteTest` comprueba `LocalContentColor`, `onPrimary` y el botón primario. **Ninguna de las dos toca `ColorMedico`, `ColorPeligro`, `ColorAlerta` ni `BorderStrong`** — que son exactamente los cuatro que fallan.

El hueco no es casual: A15 nació de un bug de *texto por defecto*, y la red de seguridad que dejó cubre ese camino. Los colores semánticos entran por otro: se pasan explícitamente como `color = X` en cada `Text`, y ninguna prueba los recorre.

> **La corrección estructural** es una prueba parametrizada sobre **la lista de pares (color, superficie, uso)**, derivada de la tabla de `03 §2.1`, en vez de una aserción escrita a mano por token. Así, un token nuevo entra a la verificación por existir, no por acordarse de añadirlo.

### 7.2 · Un token que sirve de relleno y de texto tiene dos umbrales y solo cumple uno

`ColorMedico`, `ColorPeligro` y `ColorAlerta` se usan como franja/icono/borde (3:1) **y** como color de texto (7:1). Un solo valor no puede satisfacer los dos sin volverse pastel, y esa fue exactamente la aritmética de A15 (*"la ventana es vacía: ningún color cumple las dos"*).

> **La corrección estructural** es la que A15 ya estableció como precedente: desdoblar en `X` (identidad, relleno) y `XTexto` (legibilidad sobre superficie). Tres tokens nuevos, cero cambios de identidad visual.

### 7.3 · La conformidad con Android no tiene ninguna prueba

Insets, teclado, escala de fuente y "Quitar animaciones" no están cubiertos por ninguna de las 53 pruebas. Es el único frente donde el proyecto no tiene red de seguridad, y es de donde salen `P0-1`, `P0-3`, `P1-7` y `P1-8`.

---

# 8 · Lo que ya está bien y no hay que tocar

No es cortesía: si el plan de abajo rompe algo de esto, el plan está mal.

- **`TemaAplicadoContrasteTest` lee el color realmente rasterizado, no el token.** Es la distinción que atrapó el bug de A15 y casi nadie la hace. Conservarla tal cual.
- **La arquitectura de tres canales (color / forma / texto) es real y está probada** sin que ninguna aserción dependa del color. `TarjetaDePuestoOperableEnGrisesTest` es el modelo a seguir para el resto de componentes.
- **Los cuatro estados de pantalla con `when` exhaustivo y sin rama `else`.** Un quinto estado no compila hasta recibir tratamiento propio. Es la mejor decisión estructural del frontend.
- **La barra de fatiga no se dibuja sin dato real.** `§1.3` aplicado literalmente donde era fácil no hacerlo.
- **El tema no depende del modo del sistema, y hay una prueba que lo fija.** No reintroducir un `lightColorScheme` bajo ningún concepto.
- **Ancho de franja variable (4/6/8 dp) como canal de forma** independiente del color.
- **Modal de confirmación blindado**: sin cierre por Back ni por toque fuera, bloque médico que nunca se colapsa y que **afirma** la ausencia de restricciones en vez de omitir el bloque.
- **Rendimiento sano**: `LazyColumn` con `key`, ML Kit en el dispositivo con `KEEP_ONLY_LATEST`, y el tick de 1 s reservado al cronómetro mientras el sello de frescura usa 15 s.

---

# 9 · ⚠ D-1 · La decisión que no puedo tomar

**El banner de sin conexión no tiene un par de colores válido en ninguna fuente.**

| Origen | Par | Contraste | Exige (`TypeBodyStrong`, 18 sp = texto normal) |
|---|---|---|---|
| Código actual | `#F4F7FA` sobre `#D9822B` | **2.72:1** | 7:1 ✗ (ni AA) |
| `03 §2.1` (`color.offline`) | `#8A6D3B` sobre `#2B2113` | **3.26:1** | 7:1 ✗ |

Corregir el código para que use los tokens del brief **no arregla el problema**: los tokens del propio brief tampoco cumplen. Y este banner porta la frase que `§12.1` exige literalmente — *"Pendiente de sincronización — no mover al personal hasta recuperar la red."* — que es una instrucción de seguridad operativa.

Por R2 no invento el valor. Opciones calculadas, conservando el fondo marrón que el brief eligió:

| Opción | Texto sobre `#2B2113` | Contraste | Nota |
|---|---|---|---|
| **A** | `#E8C88A` | 9.82:1 | Ámbar claro, conserva la familia cromática del token original |
| **B** | `#F0D9A8` | 11.43:1 | Más claro, máxima legibilidad, se acerca al crema |
| **C** | — | — | Subir el texto a `TypeAction` (24 sp) y aplicar 4.5:1, como se resolvió en A15 |

**Recomiendo A**: mantiene el `#8A6D3B` reconocible como familia, cumple AAA con holgura (9.82:1) y no obliga a tocar la escala tipográfica. Pero es una decisión de diseño con precedente de negocio (A15 se consultó al cliente), así que **la dejo abierta**.

---

# 10 · Plan de ejecución

Estructurado como UTs en el formato de `07_PLAN_DE_EJECUCION.md`, para que entre directo en `PROGRESO.md`. Cada una es verificable y deja prueba.

### E15 · Conformidad de plataforma y accesibilidad del cliente *(7/7)* ✅

| UT | Qué hace | LEE | VERIFICA |
|---|---|---|---|
| **E15.1** | `safeDrawingPadding()` en la `Column` raíz de `GrafoDeNavegacion` | 08 §P0-1 | Prueba instrumentada: el banner no solapa la barra de estado; el FAB queda fuera del inset inferior |
| **E15.2** | `verticalScroll` + `imePadding()` en `LoginScreen` y `PinScreen` | 08 §P0-3 | Con el teclado abierto, `login-entrar` y `pin-verificar` siguen alcanzables |
| **E15.3** | Tres tokens `…Texto` nuevos (`ColorMedicoTexto`, `ColorPeligroTexto`, `ColorAlertaTexto`) + `BorderStrong` corregido, y sustitución en los 8 puntos de uso | 08 §P0-2, §P1-3, §P1-4, §P1-5 | **Prueba parametrizada** sobre la tabla de pares (color, superficie, umbral) de `03 §2.1` — ver §7.1 |
| **E15.4** | Semántica de accesibilidad: indicador médico, marca de agua, banner | 08 §P1-1, §P2-2, §P2-3 | TalkBack anuncia *"N restricciones médicas activas"*; la marca de agua no se anuncia; el banner se anuncia una sola vez |
| **E15.5** | `.height()` → `.heightIn(min=)` en los 8 botones; altura explícita en los dos que faltan | 08 §P1-6, §P1-7 | Barrido de zonas de toque sobre **todas** las pantallas a escala 1.0 y 1.3 |
| **E15.6** | Respetar "Quitar animaciones" en los dos pulsos, dejando color pleno al desactivar | 08 §P1-8 | Con `ANIMATOR_DURATION_SCALE = 0`, el pulso no corre y el alfa queda en 1 |

### E16 · Coherencia del sistema de diseño *(0/2)* — menor prioridad

| UT | Qué hace | LEE | VERIFICA |
|---|---|---|---|
| **E16.1** | `VistaVerificando` pasa a esqueleto con texto; tokens de tamaño de icono y de esqueleto en `Dimens.kt`; retirar medidas sueltas | 08 §P2-1, §P2-4 | Ningún `.dp` literal fuera de `Dimens.kt` en `ui/`; ningún estado de carga sin texto |
| **E16.2** | Aplicar `Elevation.nivel1` a `TarjetaDePuesto` o retirar los niveles no usados de `Dimens.kt` y anotar por qué | 08 §P2-5 | `DesignTokensTest`: todo nivel de elevación declarado tiene al menos un uso |

| **E15.7** | Par de color del banner de sin conexión: `#E8C88A` sobre `#2B2113`, y **borde** en vez de fondo claro para definir el bloque | 08 §D-1 | `ContrasteSemanticoTest`: 9.82:1 de texto y 11.76:1 de borde contra el fondo de la app |

> **D-1 quedó cerrada** por el cliente durante la propia auditoría, con la opción A. Registrada como `00_DECISIONES.md §A16` y reflejada en `03_UIUX_BRIEF.md §2.1`.

---

# 11 · Trazabilidad

| Sección | Origen |
|---|---|
| §2 Veredicto de plataforma | Material 3 (insets, IME, Reduce Motion) |
| §4 P0-1, P0-3 | Material 3 · `03 §2.2` (130%) · `§12.1` |
| §4 P0-2, §5 P1-3/4/5 | `03 §5.2` · `§12.3` · `00 §A15` (precedente de método) |
| §5 P1-1, §6 P2-2/3 | `03 §5.6` · `§12.2` |
| §5 P1-6, P1-7 | `00 §A11` · `03 §5.1` |
| §5 P1-8 | Material 3 · `03 §6.3` |
| §9 D-1 | `03 §2.1` (`color.offline`) · `§12.1` (texto literal) · R2 |
| §10 Plan | `07_PLAN_DE_EJECUCION.md` (formato) |
