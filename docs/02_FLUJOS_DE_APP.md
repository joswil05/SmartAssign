# SmartAssign — Flujo de App (User Flow)

**Mapa de navegación, decisiones condicionales, estados borde y gatillos.**
Versión 1.0 · 2026-08-08

> **Cómo leer este documento.** Está separado por rol porque los dos roles no comparten ni una sola pantalla principal. Cada flujo crítico se recorre paso a paso, con sus bifurcaciones y sus salidas de error. Las reglas ambiguas están resueltas en el [Registro de Decisiones](00_DECISIONES.md); aquí se aplican, no se debaten.
>
> **Convención de nodos:**
> `[Pantalla]` · `<Decisión>` · `(Acción del servidor)` · `«Notificación»` · `⛔ Estado borde`

---

# 1 · Mapa de navegación

## 1.0 Alta del dispositivo — solo la primera vez *(F3)*

```
        (aplicación recién instalada, sin servidor configurado)
                                 │
                                 ▼
                    ┌────────────────────────────┐
                    │  [Alta de dispositivo]     │
                    │                            │
                    │  "Escanea el código que    │
                    │   te muestra el            │
                    │   Coordinador"             │
                    │                            │
                    │      [ Abrir cámara ]      │
                    └─────────────┬──────────────┘
                                  │
                    escanea el QR con la URL del servidor
                                  │
                                  ▼
                    <¿el servidor responde?>
                         │                │
                        NO               SÍ
                         │                │
                         ▼                ▼
              ⛔ "No se pudo llegar   (guardar URL)
                 a ese servidor.      ➡ continúa en 1.1
                 Revisa que estés
                 en la red de planta."
```

> **Cero tecleo, a propósito** *(§12.3)*. Escribir una dirección con guantes, de pie, es exactamente el tipo de fricción que hace que alguien pida ayuda o lo deje a medias. El escáner ya existe para los gafetes *(E1)*: reutilizarlo aquí no cuesta nada y elimina el único momento en que habría que teclear.
>
> El Coordinador genera ese QR desde `[Datos maestros] → [Alta de dispositivo]`.

## 1.1 Entrada común

```
                        ┌──────────────────┐
                        │  [Arranque]      │
                        │  splash + verif. │
                        └────────┬─────────┘
                                 │
                    <¿Hay servidor configurado?>
                         │                      │
                        NO ──► [Alta de dispositivo] (1.0)
                                                │
                                                ▼
                    <¿Sesión válida en el dispositivo?>
                         │                      │
                        NO                     SÍ
                         │                      │
                         ▼                      ▼
                  [Login usuario]      <¿Sesión bloqueada por inactividad?>
                  usuario+contraseña        │              │
                         │                 SÍ             NO
                         ▼                  │              │
              <¿Credenciales válidas?>      ▼              │
                    │        │        [Desbloqueo PIN]     │
                   NO       SÍ         4–6 dígitos         │
                    │        │              │              │
                    ▼        │       <¿PIN correcto?>      │
            ⛔ [Error auth]  │         │        │          │
             mensaje claro   │        NO       SÍ          │
             sin detalle     │         │        │          │
             de qué falló    │         ▼        │          │
                             │  ⛔ 3 intentos → │          │
                             │  vuelve a login  │          │
                             │         └────────┤          │
                             └──────────────────┴──────────┘
                                                │
                                                ▼
                                        <¿Qué rol tiene?>
                                     │                      │
                              COORDINADOR              SUPERVISOR
                                     │                      │
                                     ▼                      ▼
                          [Panel de Planta]      <¿Tiene línea asignada?>
                                                     │            │
                                                    NO           SÍ
                                                     │            │
                                                     ▼            ▼
                                          ⛔ [Sin línea]   <¿Su línea es la L8?>
                                          "No tienes línea    │          │
                                           asignada.         NO         SÍ
                                           Habla con el       │          │
                                           Coordinador."      ▼          ▼
                                          Sin más acciones  [Malla    [Panel
                                                            de línea]  Bolsón]
```

> **Nodo crítico — `<¿Tiene línea asignada?>`.** §2.3 exige que la línea la determine el sistema y que **nunca** se elija de una lista. Por eso no existe ninguna pantalla de "selección de línea" en todo el mapa. La rama `NO` es terminal: el supervisor no puede hacer absolutamente nada, y el sistema se lo dice remitiéndolo al Coordinador.

## 1.2 Rutas del Supervisor de línea

```
[Malla de línea]  ← raíz, no se puede salir salvo cerrando sesión
   │
   ├─→ [Detalle de puesto]
   │      ├─→ [Escáner de gafete] ──→ [Confirmar identidad] ──→ (asignar)
   │      ├─→ [Búsqueda manual]   ──→ [Confirmar identidad] ──→ (asignar)
   │      ├─→ [Liberar a L8]      ──→ [Confirmar] ──────────→ (despachar)
   │      ├─→ [Retiro temporal]   ──→ [Motivo] ─────────────→ (retirar)
   │      └─→ [Solicitar relevo]  ────────────────────────→ (encolar)
   │
   ├─→ [Recepciones pendientes]  ──→ [Confirmar llegada] ──→ [Destino del relevado]
   │                              └─→ [Rechazar recepción] → [Motivo obligatorio]
   │
   ├─→ [Personal de mi línea]    ──→ [Ficha de persona]  (incluye restricciones médicas)
   │
   ├─→ [Registrar paro]          ──→ [Categoría] → [Causa] → [Descripción] → (confirmar)
   │      └─→ cronómetro persistente, visible desde cualquier pantalla
   │
   ├─→ [Cerrar lote]             ──→ [Desperdicio] + [Producción] → (cerrar)
   │
   ├─→ [Panel de mi línea]       eficiencia, paros, desperdicio, cobertura
   │
   ├─→ [Avisos]                  fatiga de toda la planta, sin identidades
   │
   └─→ [Cerrar turno]            ──→ <¿bloqueado?> → lista de bloqueos
```

## 1.3 Rutas del Supervisor de L8 (Bolsón)

Su raíz es distinta *(C7)*: **no hay malla de puestos**, porque las mesas de ensamble no son puestos.

```
[Panel Bolsón]  ← raíz
   │
   ├─→ [Cola de relevos]         ordenada por B3
   │      └─→ [Detalle de solicitud]   solo puesto, nunca persona ajena (D1)
   │             ├─→ [Aceptar propuesta]  ──→ (despachar en tránsito)
   │             └─→ [Rechazar propuesta] ──→ (descartar par puesto-persona)
   │                    └─→ [Ver descartados] ──→ [Limpiar lista]
   │
   ├─→ [Recepciones pendientes]  una confirmación por persona (C8)
   │
   ├─→ [Personal en Bolsón]      su propio personal, con datos médicos
   │
   ├─→ [Avisos]
   │
   └─→ [Cerrar turno]
```

## 1.4 Rutas del Coordinador

**El Coordinador opera desde teléfono, igual que el supervisor** *(F4)*. No hay consola web: §2.1.10 exige que todo dato maestro se edite desde la aplicación, así que estas rutas tienen que funcionar con el pulgar.

```
[Panel de Planta]  ← raíz: las 10 líneas en vivo
   │
   ├─→ [Detalle de línea]        cualquiera de las 10, sin restricción
   │      └─→ [Intervenir]  ──→ [Formulario de justificación] → (ejecutar)
   │
   ├─→ [Planificación]
   │      ├─→ [Líneas y SKU del día]
   │      ├─→ [Asignar supervisores]
   │      ├─→ [Revisar cobertura prevista]
   │      └─→ [Confirmar planificación]
   │
   ├─→ [Arrancar turno]          ──→ [Confirmación] ──→ (barrido de fijos)
   │
   ├─→ [Padrón]
   │      ├─→ [Alta / edición / baja / reactivación]
   │      ├─→ [Restricciones médicas]   con vigencia (C14)
   │      ├─→ [Ausencias justificadas]
   │      ├─→ [Doble turno]
   │      └─→ [Reincorporar desde retiro temporal]   (C2)
   │
   ├─→ [Datos maestros]          líneas, puestos, SKU, turnos, supervisores, catálogos
   │      └─→ [Prioridad de líneas]     lista de 10, reordenable arrastrando (B8)
   │      └─→ [Proximidad de líneas]    elegir línea → reordenar sus 9 destinos
   │      │                              ⚠ NUNCA como cuadrícula 10×9 (A1, A3, F4)
   │      └─→ [Parámetros]              todos los de §12.6
   │      └─→ [Alta de dispositivo]     genera el QR con la URL (F3)
   │
   ├─→ [Alertas]                 escalados, tránsitos caducados, planta agotada,
   │                             supervisor no localizable
   │
   ├─→ [Histórico]               jornadas, paros, desperdicio, eficiencia
   │
   └─→ [Auditoría]               traza completa de movimientos (§12.7)
```

---

# 2 · Gatillos de interacción

Qué evento dispara qué. Lo que no está en esta tabla no cambia de pantalla por sí solo.

| Gatillo | Tipo | Qué dispara | Ref. |
|---|---|---|---|
| Toque en un puesto de la malla | Táctil | Abre `[Detalle de puesto]` | §2.2.1 |
| **Escaneo de QR de gafete** | Cámara | Resuelve ficha → `[Confirmar identidad]`. **Nunca asienta por sí solo** | §12.2, E1 |
| **Escaneo de QR de servidor** | Cámara | Configura el dispositivo. Solo en la primera instalación | F3 |
| **Notificación recibida con la app cerrada** | FCM | Despierta la app, descarga el contenido real del servidor y lo acusa | D5 |
| Confirmación deliberada en el modal | Táctil | Consolida la asignación | §12.2 |
| **Cruce de umbral *sugerido*** | Cronómetro servidor | «Aviso a **todos** los supervisores» + entra a cola de L8 | §9.4 p1, D2 |
| **Cruce de umbral *crítico*** | Cronómetro servidor | «Re-notificación única» + salta al frente de la cola | B9 |
| **Crítico sostenido sin relevo** | Cronómetro servidor | «Alerta al Coordinador» | B9 |
| Aceptación en la L8 | Táctil | Persona → tránsito · puesto → reservado · «aviso al destino» | §9.4 p3, p4 |
| **Llegada física confirmada** | Táctil | Asigna al puesto + abre `[Destino del relevado]` | §9.4 p5, p6 |
| **Vencimiento de tránsito** | Cronómetro servidor | «Alerta a origen, destino y Coordinador» + marca *Relevista demorado* | B11 |
| Confirmación de paro | Táctil | Libera rotativos → tránsitos a L8 + arranca cronómetro persistente | §11.1 |
| **Cronómetro de paro** | Temporizador UI | Permanece visible en **todas** las pantallas hasta reanudar | §11.1 |
| Cierre de lote | Táctil | Captura desperdicio + producción → recálculo y difusión | §11.3, C4 |
| **Cualquier registro del supervisor** | Servidor | Recalcula indicadores y **empuja a los dos paneles** | C4 |
| Fin de la ventana de arranque | Cronómetro servidor | Desbloquea movimientos entre líneas y desvíos | §8.4 |
| **Pérdida de conexión** | Sistema | Modo defensivo: bloquea escrituras, banner permanente | §12.1 |
| Recuperación de conexión | Sistema | Refresca datos, quita banner, reactiva acciones | §12.1 |
| Retiro de titular de puesto fijo | Táctil | Genera *vacante crítica en operación* + escalera C15 | C15 |
| Registro de un titular que llega | Táctil | Si su fijo está cubierto, ofrece *Devolver puesto al titular* | C1 |
| Inactividad prolongada | Temporizador | Bloquea sesión → `[Desbloqueo PIN]` | D6 |

> **Gatillo que deliberadamente NO existe:** ninguno que **mueva a una persona** sin acción humana. §9.3 lo prohíbe. Todos los gatillos de cronómetro de esta tabla producen **avisos, alertas o marcas** — nunca reasignaciones.

---

# 3 · Flujos críticos — Coordinador

## 3.1 Planificación de la jornada *(§8.1)*

```
[Planificación]
   │
   ├─ 1. Elegir líneas que operan y su SKU
   │       └─ <¿línea sin SKU?> → queda INACTIVA
   │             └─ (sus puestos → "fuera de operación")
   │             └─ (su personal habitual será absorbido por L8 al presentarse)
   │
   ├─ 2. Asignar un supervisor a cada línea activa
   │
   ├─ 3. Revisar cobertura prevista
   │       └─ el sistema muestra el déficit por línea contando ausencias conocidas
   │       └─ el Coordinador equilibra moviendo personal antes del turno
   │
   └─ 4. [Confirmar planificación]
           │
           └─ <¿toda línea activa tiene supervisor?>
                 │                    │
                NO                   SÍ
                 │                    │
                 ▼                    ▼
        ⛔ Rechazo nominal      (planificación confirmada)
        "L4 y L7 están activas
         sin supervisor asignado."
```

**Estados borde:**
- ⛔ **Sin datos maestros**: si no hay SKU en catálogo, la pantalla no aparece vacía — dice *"No hay SKU registrados. Regístralos en Datos maestros."* con acceso directo *(§12.4)*.
- ⛔ **Planificación ya confirmada**: se puede reabrir mientras el turno no haya arrancado; después, cualquier cambio es una intervención con justificación *(A6)*.

## 3.2 Arranque del turno y barrido de puestos fijos *(§8.3)*

```
[Arrancar turno] ──→ [Confirmación] "Vas a arrancar el turno de N líneas activas"
                          │
                          ▼
                (BARRIDO AUTOMÁTICO — servidor)
                          │
        Recorre las líneas activas por JERARQUÍA DE PRIORIDAD
        L4 → L1 → L2 → L6 → L7 → L5 → L3 → L8 → L9 → L10
                          │
              para cada PUESTO FIJO de esa línea:
                          │
              <¿Titular presente?>
                 │              │
                SÍ             NO
                 │              │
                 ▼              ▼
        (asignar titular)   <¿Hay Operador B disponible y compatible?>
        "Asignado                │                      │
         automáticamente        SÍ                     NO
         por asistencia"         │                      │
                                 ▼                      ▼
                    (asignar B + CONSERVAR      (marcar VACANTE CRÍTICA)
                     identidad del titular)      "Sin titular ni suplente
                    "Cubierto por suplente        disponible"
                     — titular ausente"          → destacada sobre las
                                                   vacantes normales
                          │
                          ▼
              (los rotativos NO se tocan: quedan LIBRES)
              (el resto del personal queda DISPONIBLE)
                          │
                          ▼
              (arranca la VENTANA DE ARRANQUE)
```

> **Por qué el orden importa** *(§8.3)*: los Operadores B son escasos. Recorriendo por prioridad, las líneas más importantes reclaman primero y el déficit cae donde menos daño hace. **Este es el único motor que usa la jerarquía de prioridad** *(A9)*.

**Estados borde:**
- ⛔ **Arranque ya ejecutado**: el botón no se muestra dos veces. Si se intenta, *"El turno ya arrancó a las HH:MM."*
- ⛔ **Barrido en curso**: pantalla de progreso con línea actual. Bloqueada contra doble toque *(§12.4)*.
- ⛔ **Sin personal presente**: el barrido corre igual y produce vacantes críticas. No falla en silencio.

## 3.3 Intervención del Coordinador *(§2.1.9, A6)*

```
[Detalle de línea] → [Intervenir] → elegir operación
   │
   ├─ Mover personal al margen de fatiga
   ├─ Extraer Operador B de otra línea (C15-N3)
   ├─ Forzar por debajo del piso de seguridad (B5)
   ├─ Saltar la ventana de arranque (B12)
   ├─ Forzar cierre de turno (C13)
   └─ Cancelar un tránsito caducado (B11)
                    │
                    ▼
      [FORMULARIO DE JUSTIFICACIÓN]  ← obligatorio, sin excepción
        · motivo de catálogo
        · texto libre (obligatorio)
                    │
          <¿formulario completo?>
             │              │
            NO             SÍ
             │              │
             ▼              ▼
      ⛔ No se ejecuta   (validar reglas duras)
                              │
                <¿restricción médica o categoría?>
                     │                    │
                  VIOLA               NO VIOLA
                     │                    │
                     ▼                    ▼
         ⛔ RECHAZO — ni el         (ejecutar + auditar)
            Coordinador puede
            saltar estas reglas
```

> **Regla dura visible en el flujo** *(§2.1.9, B12)*: la excepción del Coordinador salta fatiga y ventana de arranque. **Nunca** salta restricciones médicas ni compatibilidad de categoría.

---

# 4 · Flujos críticos — Supervisor

## 4.1 Ventana de arranque local aislado *(§8.4)* — REGLA CRÍTICA

```
El Coordinador gatilla el arranque
              │
              ▼
    (arranca la VENTANA, duración configurable)
              │
              ▼
   [Malla de línea] con banner permanente:
   "Ventana de arranque · quedan N min ·
    solo puedes registrar a quien esté en tu línea"
              │
   El supervisor va físicamente a la sala de espera,
   recoge a su grupo, lo lleva a su línea, y registra
              │
              ▼
   [Detalle de puesto] → [Escáner] → resuelve persona
              │
              ▼
   <¿Está físicamente en MI línea?>
        │                    │
       SÍ                   NO
        │                    │
        ▼                    ▼
  (continúa validación)  ⛔ RECHAZO
                         "Ventana de arranque activa.
                          [Nombre] está en la L2.
                          Quedan 6 min para poder
                          moverla entre líneas."
              │
              ▼
   DENTRO DE LA VENTANA, además:
     · movimientos entre líneas → BLOQUEADOS
     · desvíos automáticos por prioridad → DESACTIVADOS
              │
              ▼
        (fin de la ventana)
              │
              ▼
   Banner desaparece. Se abren los movimientos.
   La jerarquía de prioridad vuelve a regir.
```

> **Por qué existe** *(§8.4)*: el arranque es el momento de mayor movimiento físico de la jornada. Si el sistema empieza a desviar gente en ese momento, el resultado es gente cruzándose en direcciones contradictorias y supervisores esperando a alguien que fue redirigido. La ventana obliga a que cada supervisor **ordene primero a la gente que tiene enfrente**.

**Estados borde:**
- ⛔ **Persona ya registrada por otro**: rechazo por concurrencia *(B1)*, con nombre y línea.
- ⛔ **Persona ausente justificada**: rechazo. Nunca puede ser asignada, sin excepciones *(§6.1)*.
- ⛔ **Ventana expirada mientras el modal está abierto**: se revalida en el servidor al confirmar. La interfaz nunca decide *(§7)*.

## 4.2 Registro de un operario en puesto rotativo *(§12.2, §8.5)*

```
[Detalle de puesto libre]
        │
        ├──→ [Escáner de gafete]      ├──→ [Búsqueda manual]
        │      resuelve por el número │      por nombre o ficha
        │      impreso en el gafete   │      · solo personal DISPONIBLE
        │      resuelve QR (E1)      │      · primero los que están
        │                             │        físicamente en MI línea
        └──────────────┬──────────────┘
                       ▼
        (VALIDACIÓN CENTRAL EN EL SERVIDOR — §7.1)
        1 ¿puesto libre? 2 ¿persona disponible? 3 ¿categoría?
        4 ¿médicas? 5 ¿perfil? 6 ¿24 h? 7 ¿ventana?
        el PRIMER rechazo detiene
                       │
         <¿alguna regla falla?>
              │                │
             SÍ               NO
              │                │
              ▼                ▼
    ⛔ [Rechazo explicado]  [CONFIRMAR IDENTIDAD]
    · nombre de la regla    ┌──────────────────────────┐
    · qué hacer para        │ Nombre completo          │
      desbloquearlo         │ Ficha · Categoría        │
    · lenguaje de planta    │ ─────────────────────    │
    · texto Y forma,        │ RESTRICCIONES MÉDICAS    │
      nunca solo color      │ ACTIVAS, explícitas      │
                            │ ─────────────────────    │
                            │ [ Confirmar asignación ] │
                            └──────────────────────────┘
                                     │
                       <¿confirma deliberadamente?>
                            │              │
                           NO             SÍ
                            │              │
                            ▼              ▼
                   (no se registra   (revalidar en servidor
                    nada — el         + asignar atómicamente)
                    escaneo por             │
                    sí solo nunca           ▼
                    asienta)         (emitir evento →
                                      malla + panel + Coordinador)
```

### Escalera de sugerencia si no se eligió puesto antes *(§8.5)*

```
Registro sin puesto seleccionado
        │
        ▼
  <Nivel 1> ¿Puesto libre de esta línea del que ELLA es titular,
            cumpliendo TODAS las reglas?          → proponer
        │ no
        ▼
  <Nivel 2> ¿Ese mismo puesto, cumpliendo todo
            SALVO el perfil preferente?           → proponer, indicando que cede perfil
        │ no
        ▼
  <Nivel 3> ¿Cualquier puesto libre compatible,
            cumpliendo TODAS las reglas?          → proponer
        │ no
        ▼
  <Nivel 4> ¿Cualquier puesto libre compatible,
            SALVO el perfil preferente?           → proponer, indicando que cede perfil
        │ no
        ▼
  ⛔ NO proponer nada + NOMBRAR la regla que lo impidió
     "Sin puesto disponible: todos los libres exigen
      levantar carga, y [Nombre] la tiene restringida."
```

> **Las restricciones médicas no ceden en ningún nivel** *(§8.5, B12)*. Lo único que cede entre el nivel 1 y el 2, y entre el 3 y el 4, es el **perfil preferente** — y solo porque es una preferencia técnica, no una condición de seguridad.

## 4.3 Flujo completo de relevo *(§9.4)* — el flujo central del sistema

### Paso 1 · Detección y aviso

```
(cronómetro del servidor, umbral PROPIO del puesto — A4)
              │
    <¿cruzó el umbral SUGERIDO?>
              │ sí
              ▼
   «AVISO A TODOS LOS SUPERVISORES»
    contenido exacto (D2):
    "L4 · Puesto 3 — relevo sugerido · 62 min"
    ── sin nombre, sin ficha, sin dato de persona ──
              │
              ├─ el puesto entra a la COLA DE LA L8
              │
              └─ ⚠ EL PUESTO NO SE LIBERA
                 el operario sigue produciendo hasta
                 que llegue su reemplazo

   (alternativa: el supervisor marca MANUALMENTE
    "relevo solicitado" antes del umbral → entra a la
     cola al nivel de sugerido)
```

> **Por qué no se libera al avisar** *(§9.4)*: liberar primero y buscar después deja el puesto descubierto durante toda la búsqueda. Marcar y esperar mantiene la producción hasta el momento exacto del cambio.

### Paso 2 · Propuesta a la L8

```
[Panel Bolsón] → [Cola de relevos]
        │
   ordenada por B3:
   1. crítico antes que sugerido
   2. mayor exceso relativo sobre su PROPIO umbral (%)
   3. FIFO por antigüedad
   ── excepción: vacante crítica de puesto fijo (C15-N1)
      encabeza la cola por delante de cualquier fatiga ──
        │
        ▼
[Detalle de solicitud]  ← lo que la L8 VE (D1):
   ┌────────────────────────────────────────┐
   │ L4 · Puesto 3 · Rotativo               │
   │ Relevo crítico · 118 % del umbral      │
   │ Exige: bipedestación prolongada        │
   │ Perfil preferente: —                   │
   └────────────────────────────────────────┘
   ⚠ NUNCA muestra nombre, ficha ni restricciones
     médicas del operario a relevar

        │
   (el servidor propone el CANDIDATO — B2)
   entre el personal de L8 disponible, compatible,
   sin restricción que lo impida, NO descartado:
     1. es titular del puesto destino
     2. más tiempo en el Bolsón
     3. menor fatiga acumulada del día
     4. ficha ascendente (desempate ESTABLE)
   el perfil preferente ORDENA, no excluye
```

### Paso 3 · Aceptación o rechazo

```
        ┌──────────────┴──────────────┐
        ▼                             ▼
   [ACEPTAR]                     [RECHAZAR]
        │                             │
        ▼                             ▼
(persona → EN TRÁNSITO)      (registrar DESCARTE del par
(puesto → RESERVADO)          puesto-persona — B10)
(registrar HORA DE SALIDA)            │
        │                             ▼
        ▼                    <¿hay otra sugerencia?>
«AVISO AL SUPERVISOR              │           │
 DE LA LÍNEA DESTINO»            SÍ          NO
 "Viene [Nombre] a relevar         │           │
  el Puesto 3"                     ▼           ▼
 ── el destino SÍ ve el      (cargar la    ⛔ "No hay más
    nombre: es personal       siguiente)      candidatos
    que va a recibir ──                       compatibles
                                              disponibles."
```

> **Por qué se recuerda el rechazo** *(§9.4)*: el supervisor de la L8 puede tener motivos que el sistema no ve. Si el sistema insiste con el mismo candidato una y otra vez, deja de confiar en él.
>
> **Y por qué el descarte caduca** *(B10)*: la lista es visible con su conteo, se puede limpiar, y **caduca sola al cierre de turno** — para que un rechazo puntual no se convierta en el veto permanente e invisible contra el que la propia especificación advierte.

### Pasos 4 y 5 · Llegada y asignación

```
[Recepciones pendientes] del supervisor destino
        │
   "[Nombre] · Ficha 4821 · viene a relevar el Puesto 3"
        │
        ├──→ [CONFIRMAR LLEGADA FÍSICA]
        │         │
        │         ▼
        │   (registrar HORA DE LLEGADA — §12.7)
        │   (asignar al puesto reservado)
        │         │
        │         ▼
        │   ➡ CONTINÚA EN EL PASO 6
        │
        └──→ [RECHAZAR RECEPCIÓN]  (C10)
                  │
                  ▼
            [MOTIVO OBLIGATORIO]
             lista corta + texto opcional
                  │
                  ▼
            (persona → EN TRÁNSITO hacia L8)
             ── no directamente "en Bolsón":
                está físicamente aquí y tiene
                que caminar, y §12.7 necesita
                las dos horas ──
            (puesto → vuelve a la cola con su
             fatiga actual)
            (persona → DESCARTADA para ese puesto)
```

### Paso 6 · Reasignación en cadena del relevado *(B4, A1)*

Este es el paso que evita que el relevo consuma personal de más.

```
En el MISMO momento en que se confirma la llegada,
el sistema presenta al supervisor el destino del RELEVADO:

  <¿Hay otro puesto fatigado COMPATIBLE en ESTA línea?>
         │ sí
         ▼
   PROPONER el de MAYOR EXCESO RELATIVO
   guarda: que NO esté ya reservado por otro
           relevista en tránsito
         │
         ▼
   El supervisor lo asigna DIRECTAMENTE
   ── sin despacho, sin tránsito, sin recepción:
      no sale de la línea ──

         │ no hay
         ▼
  <Recorrer la JERARQUÍA DE PROXIMIDAD de esta línea>
   ej. L4 → L2, L1, L7, L9, L10, L6, L3, L5, L8
   en la PRIMERA línea con un puesto fatigado compatible,
   otra vez el de mayor exceso relativo
         │
         ▼
   Se ejecuta bajo DESPACHO / TRÁNSITO / RECEPCIÓN
   (Parte X) — hay desplazamiento físico real

         │ no hay en TODO el recorrido
         ▼
   DESTINO: L8 — a esperar disponible en el Bolsón
```

> **Por qué esto no reabre el efecto dominó** *(§9.2)*: el relevado no le quita el puesto a nadie que esté trabajando tranquilo. El puesto al que llega **ya estaba pidiendo relevo por su cuenta**, con o sin él. Solo se resuelve con la persona que tiene más cerca, en vez de esperar a que la L8 mande a alguien desde más lejos.

### Ejemplo normativo — capacidad limitada y relevo en cadena *(§9.4)*

Confirmado por el cliente como descripción exacta del comportamiento esperado *(A8)*:

```
5 puestos fatigados: 4 en L4, 1 en L1
La L8 solo puede cubrir 3 (médicas, categoría, o no tiene más gente)
        │
        ▼
Envía 2 → L4 · 1 → L1
Los 3 quedan en tránsito, sus 3 puestos destino RESERVADOS
        │
        ▼
Llegan los 2 relevistas a L4
        │
        ▼
Los 2 relevados NO van a la L8:
en L4 siguen fatigados otros 2 puestos
        │
        ▼
Cada relevado pasa a relevar a uno de esos 2 compañeros
        │
        ▼
La fatiga de L4 se resuelve SIN gastar más personal de la L8

(si no quedara ningún puesto fatigado disponible → L8)
(si la L8 se agota por completo → extracción inversa §9.6)
```

## 4.4 Movimiento entre líneas *(Parte X)*

```
PASO 1 — DESPACHO
[Detalle de persona] → [Despachar a línea X]
        │
   <¿está físicamente en MI línea?>       <¿está disponible?>
        │ no → ⛔ rechazo                   (en Bolsón o sin asignar)
        │                                    │ no → ⛔ rechazo
        ▼
   (registrar HORA DE SALIDA — §12.7)
   (persona → EN TRÁNSITO con destino registrado)

PASO 2 — TRÁNSITO
   ⚠ LA PERSONA ES INMUNE
   ninguna otra terminal puede capturarla ni reasignarla
        │
   <¿supera duracion_maxima_transito?>   (B11)
        │ sí
        ▼
   «ALERTA a origen, destino y Coordinador»
   puesto sigue reservado, marcado "Relevista demorado"
   ── NADIE se mueve automáticamente ──
   el Coordinador puede CANCELAR el tránsito
   → persona a "presente, sin asignar" en su última
     línea física conocida

PASO 3 — RECEPCIÓN
[Recepciones pendientes] del destino
        │
        ├─→ [CONFIRMAR LLEGADA]  → (hora de llegada) → asignar
        └─→ [RECHAZAR]           → motivo obligatorio → tránsito a L8
```

> **Por qué la confirmación es obligatoria** *(Parte X)*: sin ella, el sistema daría por ocupado un puesto desde el despacho, aunque la persona tarde cinco minutos en llegar o se quede por el camino. La confirmación es lo que mantiene alineados el sistema y la realidad física.

## 4.5 Cobertura de vacante crítica de puesto fijo en operación *(C15)*

```
(el titular Operador A se retira con el turno arrancado)
        │
        ▼
Puesto fijo → VACANTE CRÍTICA EN OPERACIÓN
«aviso al supervisor de la línea»
        │
        ▼
  <N1: ¿Operador B disponible en el BOLSÓN?>
        │ sí → solicitud a la L8, ENCABEZANDO la cola (B3)
        │      → flujo de relevo estándar
        │      → NO deja ningún hueco
        │ no
        ▼
  <N2: ¿Operador B en un rotativo de MI línea?>
        │ sí → lo ejecuta el SUPERVISOR
        │      → ese rotativo queda "ROTATIVO DESCUBIERTO"
        │      → entra a la cola A PRIORIDAD NORMAL
        │        ⚠ guarda anti-dominó: no es una emergencia nueva
        │ no
        ▼
  <N3: ¿Operador B en un rotativo de OTRA línea?>
        │ sí → SOLO lo ejecuta el COORDINADOR
        │      → con FORMULARIO DE JUSTIFICACIÓN (A6)
        │      → recorre la PROXIMIDAD desde mi línea (A1)
        │      → «notificación al supervisor de origen»
        │      → bajo despacho/tránsito/recepción (Parte X)
        │      → excepción declarada a hub-and-spoke
        │
        │      <¿la línea origen quedaría bajo su mínimo?>  (B5)
        │           │ sí → saltarla
        │           └─ si TODAS están en el piso:
        │              ⛔ «alerta al Coordinador»
        │                 puede forzar con justificación
        │ no
        ▼
  N4: NO HAY NINGÚN OPERADOR B EN PLANTA
      → vacante crítica persistente
      → «alerta al Coordinador»
```

## 4.6 Reincorporación del titular *(C1)*

```
El supervisor registra al titular que llega tarde
        │
        ▼
  <¿su puesto fijo está cubierto por un suplente?>
        │ sí
        ▼
El sistema OFRECE (no ejecuta):
   ┌──────────────────────────────────────┐
   │ El Puesto 2 está cubierto por         │
   │ [Suplente], que es Operador B.        │
   │ [ Devolver puesto al titular ]        │
   │ [ Dejar como está ]                   │
   └──────────────────────────────────────┘
        │
   <¿el supervisor acepta?>
        │ no → nada cambia (conoce contexto que el sistema no ve)
        │ sí
        ▼
   (titular → asignado a su puesto)
   micro-copia: "Titular reincorporado — suplente liberado"
        │
        ▼
   El OPERADOR B liberado NO va automáticamente a la L8:
   entra en la misma lógica del Paso 6 (B4)
     1. puesto rotativo fatigado de esta línea
     2. proximidad
     3. L8
```

## 4.7 Paro técnico *(§11.1)*

```
[Registrar paro]
   │
   ├─ 1. [Categoría]  mecánico · eléctrico · calidad · falta de material
   │
   ├─ 2. [Causa]      filtrada por la categoría elegida
   │
   ├─ 3. [Descripción]  ⚠ OBLIGATORIA
   │       "Escribe qué observaste"
   │       └─ sin texto → ⛔ no se puede confirmar
   │
   └─ 4. [Confirmar]
           │
           ▼
     (línea → EN PARO)
     (PUESTOS FIJOS → permanecen OCUPADOS:
      los operadores técnicos ejecutan la reparación)
     (PUESTOS ROTATIVOS → se LIBERAN)
           │
           ▼
     Cada operario rotativo genera SU PROPIO tránsito a L8
     (C8: la recepción en L8 es INDIVIDUAL, persona por persona)
     micro-copia en cada puesto:
     "Liberado por paro — operario en ensamble manual L8"
           │
           ▼
     ⏱ CRONÓMETRO PERSISTENTE
     visible en TODAS las pantallas de la app
     no se detiene al navegar
     solo se detiene con [Reanudar producción]
           │
           ▼
     (recalcular estadística → panel supervisor + panel Coordinador)
```

### Caso borde: relevista en tránsito hacia una línea que entra en paro *(C9)*

```
(L4 entra en paro)
(hay un relevista en tránsito hacia L4)
        │
        ▼
El tránsito NO se cancela: es INMUNE (§6.1)
        │
        ▼
Al llegar, el supervisor destino ve:
   ┌──────────────────────────────────────────┐
   │ L4 está en paro.                          │
   │ El puesto que [Nombre] venía a cubrir     │
   │ fue liberado.                             │
   │ [ Despachar a la L8 ]                     │
   └──────────────────────────────────────────┘
        │
   (si el paro ya terminó → asignación normal)
```

## 4.8 Cambio de SKU *(§11.2)*

```
[Cambiar SKU] → elegir nuevo SKU
        │
        ▼
   (CERRAR LOTE actual — captura desperdicio + producción, C5)
        │
        ▼
   (línea → EN LIMPIEZA)
        │
        ▼
   Recalcular puestos:
     · requeridos por el nuevo SKU que estaban
       "fuera de operación"  →  LIBRES
     · ya no requeridos       →  FUERA DE OPERACIÓN
          └─ si tenían ocupante → tránsito a la L8
                                  (recepción individual, C8)
        │
        ▼
   Informar: "Se activaron N puestos y se desactivaron M."
        │
        ▼
   (ABRIR LOTE nuevo)
   (línea → EN PRODUCCIÓN)
```

## 4.9 Cierre de lote *(§11.3, C4, C5)*

```
[Cerrar lote]
   │
   ├─ [Producción real del lote]     ← C4
   │
   ├─ [Desperdicio]
   │    · daño de ORIGEN   (proveedor / almacén / transporte)
   │    · daño de PROCESO  (maquinaria, descalibración)
   │      ── separados porque apuntan a responsables distintos ──
   │
   └─ [Confirmar]
        │
   <¿daño de proceso > umbral configurable del volumen total?>
        │ sí
        ▼
   ⛔ [JUSTIFICACIÓN ESCRITA OBLIGATORIA]
      sin ella no se permite el registro
        │
        ▼
   (cerrar lote + recalcular en el SERVIDOR)
        │
        ▼
   (empujar a panel del supervisor Y panel del Coordinador — C4)
```

## 4.10 Cierre de turno *(C13)*

```
[Cerrar turno]
        │
        ▼
   (VERIFICAR BLOQUEOS en el servidor)
        │
   <¿lote abierto?>  <¿gente en tránsito HACIA mi línea?>
   <¿gente mía en tránsito HACIA FUERA sin recibir?>
        │                              │
       SÍ                             NO
        │                              │
        ▼                              ▼
⛔ [CIERRE BLOQUEADO]           (EJECUTAR CIERRE)
  lista EXACTA:                   · personal → "fuera de turno"
  "No puedes cerrar todavía:      · persistir ÚLTIMO PUESTO
   · Lote 3 sigue abierto           ocupado por persona (B6)
   · [Nombre] viene en tránsito   · cancelar relevos pendientes
     desde la L8                  · liberar puestos fijos
   · [Nombre] fue despachado a    · caducar descartados (B10)
     la L2 y no ha sido recibido
     — llama al supervisor de L2"
        │
        └─ el COORDINADOR puede FORZAR
           con formulario de justificación (A6)
```

> **Por qué se bloquea** *(C13)*: cerrar con gente en tránsito deja personas caminando hacia una línea que ya no las espera. Es exactamente el problema del §1.1 —"entre que sale de una línea y llega a otra, desaparece del control"— reintroducido por la puerta de atrás.

---

# 5 · Estados borde

## 5.1 Comportamiento sin conexión *(§12.1)* — bloqueo defensivo

```
(pérdida de conexión detectada)
        │
        ▼
   BANNER PERMANENTE E INEQUÍVOCO
   ┌────────────────────────────────────────────────┐
   │ ⚠ SIN CONEXIÓN                                 │
   │ Pendiente de sincronización — no mover al      │
   │ personal hasta recuperar la red.               │
   └────────────────────────────────────────────────┘
        │
        ├─ BLOQUEADO: movimiento de personal entre líneas
        ├─ BLOQUEADO: registro de nuevas asignaciones
        ├─ BLOQUEADO: aceptar/rechazar relevos
        ├─ BLOQUEADO: confirmar recepciones
        │
        ├─ PERMITIDO: consultar la malla con últimos datos
        ├─ PERMITIDO: consultar personal de la línea (caché cifrada, D3)
        └─ PERMITIDO: ver restricciones médicas de SU personal
        │
        ▼
   Cada puesto afectado se marca visualmente
   con la advertencia del §12.1
        │
        ▼
   SELLO DE FRESCURA en cada pantalla (D4)
   "Datos de hace N min"
        │
   <¿N > antiguedad_maxima?>
        │ sí
        ▼
   Banner permanente + datos VISIBLEMENTE DEGRADADOS
```

> **Por qué bloquear en lugar de encolar** *(§12.1)*: un rechazo digital no deshace un traslado físico. Si el supervisor ya le dijo a alguien que camine a otra línea y la operación se rechaza al volver la red, el sistema y la realidad quedan desincronizados, y nadie se entera hasta que falta una persona. Es preferible impedir la orden que corregirla tarde.
>
> **Consecuencia de diseño:** no existe ninguna cola de operaciones pendientes en el dispositivo. Un intento bloqueado **no queda pendiente de envío**.

## 5.2 Los cuatro estados de pantalla *(§12.4)*

**Regla dura de interfaz: ninguna pantalla puede quedar vacía sin explicación, y "cargando" y "vacío" nunca se ven igual.**

| Estado | Representación | Ejemplo |
|---|---|---|
| **Cargando** | Esqueleto animado con la forma del contenido real. **Nunca** una pantalla en blanco | Malla con siluetas de puesto pulsando |
| **Vacío legítimo** | Icono + explicación + siguiente paso | *"Ningún puesto en fatiga ahora mismo."* |
| **Fuera de operación** | Tratamiento visual **propio**, ni libre ni ocupado | *"Puesto no requerido por el SKU de hoy"* |
| **Error** | Causa concreta + acción concreta. Nunca código ni texto genérico | *"No se pudo cargar la línea. Reintentando en 5 s."* |

> **Por qué importa tanto** *(§12.4)*: una línea que aún no responde y una línea sin nadie asignado se ven igual si no se distinguen, y eso lleva al supervisor a reasignar personal que ya estaba colocado.

## 5.3 Acción en curso y doble toque *(§12.4)*

```
[Botón de acción]
   → toque
   → ⏳ botón bloqueado + indicador visible de progreso
   → resultado explícito (éxito o rechazo explicado)
```

> **Por qué** *(§12.4)*: sin retroalimentación, el reflejo ante la demora es volver a tocar. Y estas operaciones **no son repetibles sin consecuencia**: se piden dos relevos, se despacha dos veces, se registra la baja dos veces.

**Implementación obligatoria:** toda operación de escritura viaja con clave de idempotencia; un reintento con la misma clave devuelve el mismo resultado, no ejecuta dos veces.

## 5.4 Catálogo de rechazos

Todo rechazo nombra la causa y el siguiente paso, en lenguaje de planta *(§1.3, §12.4)*.

| Situación | Mensaje |
|---|---|
| Puesto ya ocupado | *"El Puesto 3 acaba de ser ocupado por [Nombre]."* |
| Persona ya capturada *(B1)* | *"[Nombre] acaba de ser registrado en L4 · Puesto 3 por otro supervisor."* |
| Categoría incompatible | *"[Nombre] es Operador A. Este puesto es rotativo y los Operadores A no bajan a puestos rotativos."* |
| **Restricción médica** *(§7.2)* | *"[Nombre] tiene restringido levantar carga y este puesto lo exige. No se puede asignar."* |
| Regla de 24 h *(A4)* | *"[Nombre] estuvo en Girar botellas en su jornada anterior. No puede repetirlo."* |
| Ventana de arranque *(§8.4)* | *"Ventana de arranque activa. [Nombre] está en la L2. Quedan 6 min."* |
| Persona en tránsito *(§6.1)* | *"[Nombre] va en camino a la L7. No se puede reasignar durante el trayecto."* |
| Ausente justificado *(§6.1)* | *"[Nombre] está de vacaciones. No puede ser asignado."* |
| Sin conexión *(§12.1)* | *"Sin conexión. No se puede mover personal hasta recuperar la red."* |
| Escalera sin resultado *(§8.5)* | *"Sin puesto disponible: todos los libres exigen [capacidad], que [Nombre] tiene restringida."* |
| Sin candidatos en L8 | *"No hay más candidatos compatibles disponibles en el Bolsón."* |
| Planta agotada *(§9.6)* | *"Capacidad crítica de planta agotada. Requiere intervención humana."* |
| Cierre bloqueado *(C13)* | Lista exacta de bloqueos con a quién llamar |

## 5.5 Micro-copia contextual de puesto *(§12.5)*

**Literal de la especificación. No se reinventa.**

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

Micro-copia **añadida** por decisiones posteriores, marcada como no procedente de la fuente:

| Situación | Mensaje | Origen |
|---|---|---|
| Rotativo descubierto | *"Sin ocupante — pendiente de cubrir"* | C11 |
| Puesto reservado | *"Reservado — [Nombre] viene en camino"* | §9.4 p3 |
| Tránsito demorado | *"Relevista demorado — supera el tiempo previsto"* | B11 |
| Vacante crítica en operación | *"Máquina sin operador — titular retirado"* | C15 |

---

# 6 · Máquina de estados

## 6.1 Estados del trabajador *(Parte VI)*

```
                    ┌──────────────────┐
                    │  FUERA DE TURNO  │ ◄──── cierre de turno
                    └────────┬─────────┘
                             │ registro por supervisor
                             │ o marca del Coordinador (C3)
                             ▼
                  ┌────────────────────────┐
        ┌────────►│ PRESENTE, SIN ASIGNAR  │◄────────┐
        │         └───────┬────────────────┘         │
        │                 │ asignación                │
        │                 ▼                           │
        │         ┌────────────────┐                  │
        │         │    ASIGNADO    │                  │
        │         └───┬────────┬───┘                  │
        │             │        │                      │
        │   liberar   │        │ despacho             │
        │   a L8      │        ▼                      │
        │             │  ┌──────────────┐             │
        │             │  │ EN TRÁNSITO  │             │
        │             │  │  ⚠ INMUNE    │             │
        │             │  └──┬────────┬──┘             │
        │             │     │        │ cancelación    │
        │             │     │        │ del Coordinador│
        │             │     │        │ (B11) ─────────┘
        │             │     │ recepción confirmada
        │             │     ▼
        │             │  ┌────────────────┐
        │             └─►│   EN BOLSÓN    │
        │                └───────┬────────┘
        │                        │ propuesta aceptada
        │                        └──► EN TRÁNSITO
        │
        │  reincorporación
        │  del Coordinador (C2)
        │         ┌──────────────────────────┐
        └─────────┤ RETIRADO TEMPORALMENTE   │◄─── retiro (§9.7)
                  └──────────────────────────┘

                  ┌──────────────────────────┐
                  │  AUSENTE JUSTIFICADO     │  ⛔ NUNCA asignable
                  └──────────────────────────┘     sin excepciones
```

**Reglas de la máquina** *(§6.1)*:
- **El tránsito es inmune.** Ninguna otra terminal puede capturar ni reasignar a quien está caminando.
- **Quien está fuera de turno SÍ puede ser asignado.** Es la situación normal de quien cerró ayer y hoy se presenta.
- **Quien está ausente justificado NUNCA puede ser asignado.** `[REGLA DURA]`
- **Quien está asignado, en tránsito o retirado no puede recibir otra asignación.**
- **Ningún estado carece de salida.** Se cerraron las dos que faltaban: retiro temporal *(C2)* y tránsito colgado *(B11)*.

## 6.2 Estados del puesto *(§5.3, C11)*

```
FUERA DE OPERACIÓN ──(SKU lo requiere)──► LIBRE
       ▲                                     │
       │                                     │ asignación
   (SKU deja de                              ▼
    requerirlo)                          OCUPADO
       │                                     │
       └─────────────────────────────────────┤
                                             │ fatiga cruza umbral
                       ┌─────────────────────┤
                       ▼                     │
              RELEVO PENDIENTE               │ liberación
              (sigue OCUPADO,                │
               sigue produciendo)            ▼
                       │                  LIBRE
                       │ L8 acepta           │
                       ▼                     │ nadie lo cubre
                  RESERVADO                  ▼
                       │            ROTATIVO DESCUBIERTO (C11)
                       │ llegada
                       ▼
                   OCUPADO

Solo puestos FIJOS:
LIBRE ──(sin titular ni suplente en el barrido)──► VACANTE CRÍTICA
OCUPADO ──(titular se retira en operación)──► VACANTE CRÍTICA EN OPERACIÓN (C15)
```

## 6.3 Estados de la línea *(§3.1)*

```
INACTIVA ──(SKU planificado)──► ACTIVA
                                   │
                                   ▼
                             EN ARRANQUE ──► EN PRODUCCIÓN
                                                │      ▲
                                     paro ──────┤      │ reanudar
                                                ▼      │
                                            EN PARO ───┘
                                                │
                                cambio de SKU ──┤
                                                ▼
                                          EN LIMPIEZA ──► EN PRODUCCIÓN
```

---

# 7 · Trazabilidad

| Flujo | Origen |
|---|---|
| 1.1 Entrada y rol | §2.3, D6 |
| 1.3 Rutas de L8 | C7, §2.2.7 |
| 2 Gatillos | §9.4, §11.1, §12.1, §12.4, B9, B11, C4 |
| 3.1 Planificación | §8.1 |
| 3.2 Arranque y barrido | §8.3, A9 |
| 3.3 Intervención | §2.1.9, A6, B12 |
| 4.1 Ventana de arranque | §8.4 |
| 4.2 Registro y escalera | §12.2, §8.5, §7.1, ⚠ E1 |
| 4.3 Relevo completo | §9.1–§9.4, A1, A8, B2, B3, B4, B10, D1, D2 |
| 4.4 Movimiento entre líneas | Parte X, §12.7, B11 |
| 4.5 Vacante crítica en operación | C15, A6, B5 |
| 4.6 Titular reincorporado | C1, §12.5 |
| 4.7 Paro | §11.1, C8, C9 |
| 4.8 Cambio de SKU | §11.2, C5 |
| 4.9 Cierre de lote | §11.3, C4, C5 |
| 4.10 Cierre de turno | C13, B6, B10 |
| 5.1 Sin conexión | §12.1, D3, D4 |
| 5.2–5.3 Estados y doble toque | §12.4 |
| 5.4 Rechazos | §1.3, §12.4, todas las reglas |
| 5.5 Micro-copia | §12.5 (literal), C11, B11, C15 |
| 6 Máquinas de estado | Parte VI, §5.3, §3.1, B11, C2, C11, C15 |
