# SmartAssign — Technical Requirement Document

**Stack, integraciones y API, seguridad y rendimiento, políticas de escalabilidad.**
Versión 1.0 · 2026-08-09

> **De dónde parte.** Dos decisiones son fijas y no se negocian: **Android** (los dos roles operan desde teléfono, incluido el Coordinador — F4) y **SQL Server** (estándar ya operado por la empresa). La tercera —**la API intermedia**— fue delegada al equipo técnico y **se confirma tras el análisis de §1.2**: el teléfono nunca se conecta directo a la base, y esa es la razón por la que desplegar un dispositivo cuesta una URL.
>
> Regla de este documento: **ninguna decisión técnica sin la regla de negocio que la exige.** Si una elección no puede citar un `§` o una decisión del registro, es preferencia, y se marca como tal.

---

# 1 · Definición del Tech Stack

## 1.1 Resumen

| Capa | Elección | Motivo dominante |
|---|---|---|
| **Cliente** | Kotlin + Jetpack Compose | Anexo §1 · condiciones de uso §12.3 |
| **Backend** | **ASP.NET Core 10 (C#)** | Ecosistema SQL Server · tiempo real nativo · TI ya formado |
| **ORM** | **EF Core 10** + Dapper para lecturas calientes | Migraciones versionadas · consultas de panel sin sobrecoste |
| **Base de datos** | SQL Server 2019+ | Anexo §1 — decisión cerrada |
| **Tiempo real** | **SignalR** sobre WebSocket | §2.1.5, C4 — sin infraestructura adicional |
| **Caché local** | Room + SQLCipher | §12.1, §12.2, D3 |
| **Escaneo** | CameraX + ML Kit **QR** en dispositivo | §12.1, E1 |
| **Autenticación** | JWT propio + AD/Entra ID opcional | D6 |
| **Notificaciones** | **FCM como campana vacía** | D5 |
| **Distribución** | **Autoalojada en el servidor de planta** | F3 |

## 1.2 Por qué existe la API intermedia

El anexo la prescribe, pero el cliente delegó explícitamente la decisión: *"haz lo que creas más conveniente, que haga que el despliegue en supervisores y coordinadores sea más sencillo y sin tantas complicaciones."*

**Conclusión tras el análisis: la API se mantiene, y se mantiene precisamente porque es lo que hace el despliegue sencillo.** No es una carga heredada; es la pieza que evita cargar cada teléfono con configuración.

### La alternativa "app directo a SQL Server" no existe

| Obstáculo | Consecuencia real en despliegue |
|---|---|
| **Sin driver soportado** | Microsoft no publica driver de SQL Server para Android. Las alternativas están abandonadas |
| **Credenciales en el APK** | Cada teléfono llevaría usuario y contraseña de la base. **Rotar esa contraseña obligaría a redistribuir el APK a todos los dispositivos el mismo día** |
| **Puerto expuesto** | Habría que abrir el puerto de SQL Server a los teléfonos de la Wi-Fi de planta |
| **Parámetros §12.6** | Umbrales de fatiga, ventana de arranque, piso de seguridad vivirían en el dispositivo: **cambiar un umbral exigiría una versión nueva de la app** |
| **Decisión en dispositivo (§7)** | Las reglas médicas viajarían en cada APK; un cliente modificado podría saltarlas |
| **Estado en vivo (§2.1.5, C4)** | Los paneles se actualizan porque alguien **empuja**. Once teléfonos consultando la base en bucle es peor en todo |
| **Notificaciones (D5)** | FCM se envía **desde un servidor**. Un teléfono no notifica a otro |

### Lo que la API le ahorra a cada dispositivo

> **Configuración total de un teléfono nuevo: una URL.** Sin credenciales de base, sin cadena de conexión, sin driver, sin certificado de cliente. Y esa URL no se teclea: se escanea de un QR que muestra el Coordinador *(F3)* — con el mismo escáner que ya existe para los gafetes.

### Dónde sí se simplifica de verdad

La simplificación va en el servidor, no en quitar la API:

```
Servidor de planta
├── SmartAssign.Api     ← UN ejecutable autocontenido, servicio de Windows
└── SQL Server          ← ya existe en la empresa

Infraestructura nueva total: un servicio en un servidor.
```

**Descartado por innecesario a esta escala:** contenedores, orquestación, microservicios, colas de mensajes, caché distribuida, servidor de identidad separado.

> Son **11 dispositivos concurrentes**. Cada pieza añadida es una que alguien tiene que mantener a las tres de la mañana cuando un supervisor no puede colocar a nadie.

## 1.3 Backend: por qué ASP.NET Core

El anexo no prescribe framework de servidor. La evaluación:

| Criterio | **ASP.NET Core** | Node/NestJS | Spring Boot |
|---|---|---|---|
| Integración con SQL Server | Nativa, de primera clase | Buena vía driver | Buena vía JDBC |
| Tiempo real *(§2.1.5, C4)* | **SignalR integrado**, sin servicio extra | Socket.io / ws, biblioteca aparte | STOMP/WebSocket, más configuración |
| Transacciones y procedimientos *(§7.5)* | Excelente, con `TransactionScope` y SPs de primera | Correcta | Excelente |
| Despliegue on-premise Windows *(⚠E3)* | **Nativo, IIS o servicio de Windows** | Requiere Node en el servidor | Requiere JVM |
| Personal de TI que ya lo opera | **Alta probabilidad**: la empresa ya vive en ecosistema SQL Server | Media | Media |
| Tipado fuerte para reglas de seguridad | **Fuerte, con `record` y tipos no nulos** | TypeScript, borrado en ejecución | Fuerte |
| Coste de licencia | Gratuito, multiplataforma | Gratuito | Gratuito |

**Decisión: ASP.NET Core 10** — es la versión del SDK ya instalado en la máquina de desarrollo.

> **El criterio que desempata no es técnico, es de continuidad.** El anexo justifica SQL Server porque *"licencias, respaldos, personal de TI ya familiarizado"*. Ese mismo argumento aplica al servidor: un equipo que opera SQL Server opera Windows Server, y probablemente .NET. Elegir Node o Java introduciría una segunda cadena de herramientas que ese equipo tendría que aprender **para mantener un sistema del que depende la seguridad ocupacional de 160 personas**.
>
> El segundo criterio es SignalR: §2.1.5 exige ver las 10 líneas en vivo y C4 exige que **todo registro se refleje en los dos paneles**. Con ASP.NET Core eso es una dependencia menos.

## 1.4 ORM: EF Core con Dapper para lecturas

**EF Core 10** para el modelo y las escrituras. **Dapper** para consultas de panel y colas.

| Uso | Herramienta | Motivo |
|---|---|---|
| Migraciones versionadas | EF Core | §11 de [04](04_ESQUEMA_BACKEND.md): expansión y contracción |
| Escrituras de negocio | **Procedimientos almacenados vía EF Core** | La app no tiene `INSERT` directo sobre `Asignacion` ni `Movimiento` |
| Lecturas de panel *(C4)* | Dapper | Consultas con agregados que EF traduce mal |
| Cola de relevos *(B3)* | Dapper | Ordenación por exceso relativo calculado |

> **Matiz importante:** el ORM **no es** donde viven las reglas de negocio. [04 §7](04_ESQUEMA_BACKEND.md) establece que la escritura pasa obligatoriamente por procedimientos almacenados, con `DENY INSERT` a la cuenta de aplicación. EF Core aquí es un mapeador y un motor de migraciones, no el guardián de las reglas.
>
> **Por qué:** §7 exige que *"la decisión final nunca quede del lado del dispositivo"*. Si la validación viviera en el ORM, bastaría otro cliente con las mismas credenciales para saltarla. En el procedimiento almacenado, no.

## 1.5 Cliente Android

| Componente | Elección | Motivo |
|---|---|---|
| Lenguaje | Kotlin | Estándar Android |
| UI | Jetpack Compose + Material 3 | Los estados del §5.3 y §12.4 se modelan mejor de forma declarativa |
| Arquitectura | Clean Architecture + MVVM | §4 de este documento |
| Inyección | Hilt | Estándar del ecosistema |
| Asincronía | Coroutines + Flow | El estado en vivo de C4 es un flujo, no una consulta |
| Red | Retrofit + OkHttp + kotlinx.serialization | — |
| Tiempo real | Cliente SignalR para Java/Kotlin | C4, D5 |
| Persistencia local | **Room + SQLCipher** | D3: datos médicos cifrados |
| Claves | Android Keystore | D3 |
| Cámara | CameraX | §12.2 |
| Escaneo | **ML Kit Barcode en modo QR, modelo en dispositivo** | §12.1, E1 |
| Notificaciones | **FCM** (campana vacía) | D5 |
| Trabajo en segundo plano | WorkManager para tareas puntuales | — |
| `minSdk` / `targetSdk` | **26** / **36** | ⚠ `PENDIENTE-E4` · SDK 34/36 disponibles en la máquina |

> **ML Kit en modo dispositivo, no en la nube.** El modelo empaquetado se ejecuta localmente y **no realiza ninguna llamada de red**: la imagen del gafete nunca sale del teléfono. La variante en la nube queda **prohibida** en este proyecto *(§12.1)*.
>
> **Ya no hace falta servicio en primer plano.** Con FCM *(D5)*, la app no necesita mantenerse viva para recibir avisos. Se elimina el servicio permanente, la notificación persistente, el arranque en `BOOT_COMPLETED`, el watchdog y la exención de batería.

### El escáner tiene dos usos, no uno

| Uso | Qué escanea |
|---|---|
| **Identificación de personal** | QR del gafete, que codifica el número de ficha *(§12.2, E1)* |
| **Alta del dispositivo** | QR con la URL del servidor que muestra el Coordinador *(F3)* |

> Reutilizar el escáner para la configuración inicial significa **cero tecleo** en el único momento en que un supervisor tendría que escribir una URL a mano, de pie y con un teléfono nuevo.

---

# 2 · Integraciones y APIs

## 2.1 Servicios externos: uno, y sin datos de personal

> §12.1: **"Ningún dato de personal puede salir hacia servicios de terceros."**

| Necesidad | Solución habitual | **Aquí** |
|---|---|---|
| **Notificaciones push** | Firebase Cloud Messaging | **FCM como campana vacía** *(D5)* — ping sin contenido de negocio |
| Reconocimiento de QR | API en la nube | **ML Kit en dispositivo** |
| Telemetría de fallos | Crashlytics / Sentry alojado | **Registro local + envío al servidor propio** |
| Analítica | Google Analytics | **Ninguna** |
| Tipografías | Google Fonts | **Roboto del sistema** |
| Distribución del APK | Play Store / App Distribution | **Autoalojada en el servidor de planta** *(F3)* |
| Mapas / geolocalización | — | **No se usa** |

**FCM es el único servicio externo, y no transporta ningún dato de personal.** La carga útil que sale del servidor es un identificador opaco de evento; el contenido real se descarga del servidor de planta por HTTPS. Lo único que sale hacia Google es el **token del dispositivo**, que identifica a un teléfono, no a una persona de la plantilla.

**Verificación automatizada:** una prueba falla el build si la configuración de red contiene cualquier host que no sea el servidor de planta o los extremos de FCM, y otra verifica que **la carga útil de FCM no contiene ningún campo de negocio** *(§2.5)*.

## 2.2 Estilo de API: REST + SignalR

**REST** para comandos y consultas; **SignalR** para el estado en vivo. **GraphQL descartado.**

> **Por qué REST y no GraphQL:** el argumento de GraphQL es que el cliente elija qué campos recibe. Aquí eso es exactamente lo que **no** queremos: D1 establece que el supervisor de L8 ve un subconjunto estrictamente definido de un puesto ajeno. Con endpoints REST, esa proyección la fija el servidor y es auditable de un vistazo. Con GraphQL, el control de qué campos puede pedir cada rol se convierte en configuración adicional que hay que mantener correcta — más superficie donde equivocarse con datos médicos.

### Convenciones

- Base: `https://{host}/api/v1`
- Solo el **encabezado** identifica la versión mayor; los cambios compatibles no rompen clientes *(Anexo §3)*.
- Toda escritura exige `Idempotency-Key` *(§12.4)*.
- Todo error devuelve `application/problem+json` con **mensaje en lenguaje de planta** *(§1.3)*.
- Fechas en ISO-8601 UTC. **La hora del dispositivo no se acepta como dato** *(C6)*.

### Formato de error

```json
{
  "type": "https://smartassign.local/errors/restriccion-medica",
  "title": "Restricción médica",
  "status": 422,
  "codigo": "RESTRICCION_MEDICA",
  "detail": "María López tiene restringido levantar carga y este puesto lo exige. No se puede asignar.",
  "siguientePaso": "Elige otro puesto o consulta con el Coordinador."
}
```

> `detail` y `siguientePaso` son **obligatorios** en todo error. §12.4: *"Todo rechazo explica la causa y el siguiente paso, en lenguaje de planta. Nunca códigos de error ni mensajes genéricos."* El campo `codigo` es para el cliente y los registros; **nunca se muestra al usuario**.

## 2.3 Catálogo de endpoints

### Autenticación *(D6)*

| Método | Ruta | Notas |
|---|---|---|
| `POST` | `/auth/login` | Devuelve access + refresh. **No devuelve línea**: se resuelve por petición *(§2.3)* |
| `POST` | `/auth/refresh` | Refresh ligado a `device_id` |
| `POST` | `/auth/pin/verify` | Reentrada durante el turno |
| `POST` | `/auth/logout` | Revoca sesión y **ordena purga de la caché local** *(D3)* |
| `GET` | `/auth/me` | Rol, nombre y **línea vigente** |

### Dispositivo y notificaciones *(D5, F3)*

| Método | Ruta | Notas |
|---|---|---|
| `GET` | `/app/version` | Versión vigente y `version_minima_api`. **Anónimo**: se consulta antes de tener sesión |
| `GET` | `/app/apk` | Descarga del instalable desde el propio servidor |
| `GET` | `/servidor/info` | Verificación al escanear el QR de alta: confirma que la URL responde |
| `POST` | `/dispositivos/push-token` | Registra o renueva el token de mensajería |
| `GET` | `/notificaciones/{id}` | **Contenido real** de la notificación, tras despertar por el ping |
| `POST` | `/notificaciones/{id}/acuse` | Marca acusada. Sin esto, la crítica escala *(D5)* |
| `GET` | `/notificaciones/pendientes` | Sincronización al volver al primer plano |

### Supervisor — línea

| Método | Ruta | Regla |
|---|---|---|
| `GET` | `/lineas/mi-linea` | Alcance forzado por servidor *(§2.2)* |
| `GET` | `/lineas/mi-linea/puestos` | Incluye estado, fatiga relativa y micro-copia *(§12.5)* |
| `GET` | `/lineas/mi-linea/personal` | Solo su línea. Incluye médicas *(§2.2.9)* |
| `GET` | `/personal/buscar?q=` | **Solo disponibles**, los de su línea primero *(§12.2)* |
| `GET` | `/personal/por-ficha/{ficha}` | Resolución del escaneo *(§12.2)* |
| `POST` | `/puestos/{id}/asignar` | Valida las 7 reglas *(§7.1)*. Requiere confirmación previa |
| `POST` | `/puestos/{id}/liberar` | Destino L8 *(§9.7)* |
| `POST` | `/puestos/{id}/retiro-temporal` | *(§9.7)* |
| `POST` | `/puestos/{id}/solicitar-relevo` | *(§9.4 p1)* |
| `POST` | `/puestos/{id}/devolver-titular` | *(C1)* |
| `POST` | `/puestos/{id}/cubrir-vacante-critica` | N2 de *(C15)* |
| `GET` | `/asignaciones/sugerencia?personalId=` | Escalera *(§8.5)* |

### Relevos — L8 *(§9.4)*

| Método | Ruta | Regla |
|---|---|---|
| `GET` | `/relevos/cola` | **Solo supervisor de L8.** Proyección de `vw_SolicitudRelevo_L8` *(D1)*. Orden de *(B3)* |
| `GET` | `/relevos/{id}/candidato` | Ranking *(B2)*, excluye descartados |
| `POST` | `/relevos/{id}/aceptar` | Tránsito + reserva atómicos |
| `POST` | `/relevos/{id}/rechazar` | Registra descarte del par *(B10)* |
| `GET` | `/relevos/{id}/descartados` | Visible con conteo *(B10)* |
| `DELETE` | `/relevos/{id}/descartados` | Limpieza. Solo L8 o Coordinador |

### Movimientos *(Parte X)*

| Método | Ruta | Regla |
|---|---|---|
| `POST` | `/movimientos/despachar` | Registra `hora_salida` *(§12.7)* |
| `GET` | `/movimientos/recepciones-pendientes` | **Individual**, una por persona *(C8)* |
| `POST` | `/movimientos/{id}/confirmar-llegada` | `hora_llegada` + sugerencia de destino del relevado |
| `POST` | `/movimientos/{id}/rechazar-recepcion` | **Motivo obligatorio** *(C10)* |
| `GET` | `/movimientos/{id}/destino-relevado` | *(B4)* |

### Operación

| Método | Ruta | Regla |
|---|---|---|
| `POST` | `/paros` | Descripción obligatoria *(§11.1)* |
| `POST` | `/paros/{id}/reanudar` | Detiene el cronómetro |
| `GET` | `/lotes/abierto` | *(C5)* |
| `POST` | `/lotes/{id}/avance` | Producción parcial *(C4)* |
| `POST` | `/lotes/{id}/cerrar` | Desperdicio + producción. Justificación sobre umbral *(§11.3)* |
| `POST` | `/lineas/mi-linea/cambiar-sku` | *(§11.2)* |
| `GET` | `/lineas/mi-linea/estadistica` | Calculada **en servidor** *(C4)* |
| `POST` | `/turnos/cerrar` | Verifica bloqueos *(C13)* |

### Coordinador

| Método | Ruta | Regla |
|---|---|---|
| `GET` | `/planta/estado` | Las 10 líneas en vivo *(§2.1.5)* |
| `POST` | `/planificacion` | *(§8.1)* |
| `POST` | `/planificacion/confirmar` | Rechaza si hay línea activa sin supervisor |
| `POST` | `/turnos/arrancar` | Dispara el barrido *(§8.3)* |
| `GET`/`PUT` | `/maestros/prioridad-lineas` | Versionado *(B8)* |
| `GET`/`PUT` | `/maestros/proximidad-lineas` | Tabla 10×9 *(A1, A3)* |
| `GET`/`PUT` | `/maestros/parametros` | *(§12.6)* |
| `GET`/`POST`/`PUT` | `/maestros/personal` | Padrón *(§2.1.6)* |
| `POST` | `/maestros/personal/{id}/restricciones` | Con vigencia *(C14)* |
| `POST` | `/personal/{id}/reincorporar` | Desde retiro temporal *(C2)* |
| `POST` | `/intervenciones` | **Exige justificación** *(A6)* |
| `POST` | `/movimientos/{id}/cancelar` | Tránsito caducado *(B11)* |
| `GET` | `/auditoria` | *(§12.7)* |
| `GET` | `/historico/...` | *(§2.1.11)* |
| `POST` | `/maestros/version-app` | Publica una versión nueva del APK *(F3)* |

## 2.4 Canal en vivo (SignalR)

**Concentrador único** `/hub/planta` con grupos por alcance — el grupo es lo que garantiza el aislamiento del §2.2 a nivel de transporte:

| Grupo | Miembros |
|---|---|
| `linea:{id}` | El supervisor de esa línea + el Coordinador |
| `planta` | Solo el Coordinador |
| `bolson` | El supervisor de L8 + el Coordinador |
| `avisos` | **Todos** los supervisores — solo eventos sin identidad *(D2)* |

### Eventos

| Evento | Grupo | Contenido |
|---|---|---|
| `PuestoActualizado` | `linea:{id}` | Estado, ocupante, fatiga, micro-copia |
| `FatigaAvanzada` | `linea:{id}` | Exceso relativo *(A4)* |
| `AvisoFatigaPlanta` | `avisos` | **`"L4 · Puesto 3 — relevo sugerido · 62 min"`. Sin identidad** *(D2)* |
| `RelevoEnCola` | `bolson` | Proyección D1 |
| `TransitoEntrante` | `linea:{id}` | Nombre — el destino sí lo ve, va a recibirla |
| `TransitoDemorado` | `linea:{id}`, `bolson`, `planta` | *(B11)* |
| `EstadisticaActualizada` | `linea:{id}`, `planta` | **Recalculada en servidor** *(C4)* |
| `ParoIniciado` / `ParoReanudado` | `linea:{id}`, `planta` | *(§11.1)* |
| `AlertaCoordinador` | `planta` | Escalados, planta agotada, supervisor no localizable |

> **El aislamiento se aplica al suscribirse, no al emitir.** El servidor asigna los grupos a partir de `Linea.supervisor_actual`; el cliente **no puede pedir** unirse a un grupo. Un cliente manipulado que solicite `linea:4` sin ser su supervisor recibe rechazo de suscripción.
>
> `AvisoFatigaPlanta` es el único evento que llega a todos los supervisores, y por eso su carga útil está restringida por contrato: si algún día alguien añade el nombre del operario a ese evento, viola §2.2 para nueve supervisores a la vez. Hay una prueba automatizada que lo verifica.

## 2.5 Notificaciones — FCM como campana vacía *(D5)*

> **Requisito del cliente:** las notificaciones deben llegar **sí o sí**, aunque la app no esté abierta. Y: *"haz lo que sea más conveniente sin complicar las cosas."*

### El mecanismo

```
Servidor de planta
   │  1. ocurre un evento (relevo aceptado, tránsito entrante, alerta)
   │
   ├──► inserta en Notificacion (tabla propia, contenido completo)
   │
   └──► envía a FCM un ping SIN CONTENIDO DE NEGOCIO
            {"data": {"e": "a91f3c"}}
            ── ni nombre, ni ficha, ni línea, ni puesto ──
                        │
                        ▼
                      FCM ──► teléfono (app cerrada, en reposo, tras reinicio)
                        │
                        ▼
              la app despierta y pide el contenido real
              GET /notificaciones/a91f3c   → servidor de planta, HTTPS, con JWT
                        │
                        ▼
              "Viene María López a relevar el Puesto 3"
                        │
                        ▼
              POST /notificaciones/a91f3c/acuse
```

**Contrato de carga útil `[SEGURIDAD DE DATOS]`:** el mensaje FCM contiene **exclusivamente** un identificador opaco. Hay una prueba automatizada que inspecciona el objeto enviado y **falla el build si aparece cualquier campo de negocio**. Sin esa prueba, el primero que quiera "mejorar la experiencia" añadirá el nombre al título de la notificación y sacará datos de personal hacia un tercero sin que nadie lo note.

### Por qué esto es lo más simple

FCM entrega con la app cerrada, con el teléfono en reposo (Doze) y tras un reinicio, **sin nada de esto**:

| Se elimina | |
|---|---|
| Servicio en primer plano con notificación permanente | ✅ fuera |
| Arranque en `BOOT_COMPLETED` | ✅ fuera |
| Exención de optimización de batería | ✅ fuera |
| Watchdog con alarma exacta | ✅ fuera |
| **MDM / Device Owner** | ✅ fuera — era la dependencia más cara del proyecto |

Era la parte más frágil de la arquitectura y desaparece entera. Además, deja de importar el fabricante del teléfono: las políticas agresivas de ciertas marcas matan servicios en segundo plano, pero **no** bloquean FCM de alta prioridad.

### Capa de garantía — lo único que se conserva

El requisito es *"sí o sí"*, y FCM entrega con fiabilidad muy alta pero no garantizada. Por eso se mantiene el acuse:

- El servidor marca cada notificación como **entregada / acusada / escalada**.
- Una notificación **crítica** sin acuse en el tiempo configurado **escala al Coordinador** y aparece en su panel como *"supervisor no localizable"*.

> **Es barato y es lo que hace verificable el "sí o sí".** No impide que un mensaje se pierda; impide que alguien **crea que llegó cuando no llegó**. Es el §1.3 aplicado a la infraestructura: el sistema nunca miente sobre lo que está pasando.

### Relación con SignalR

No son dos sistemas: son dos tramos del mismo.

| Situación de la app | Canal |
|---|---|
| **Abierta y en uso** | SignalR — el panel en vivo lo necesita de todas formas *(C4)* |
| **Segundo plano o cerrada** | **FCM** → despierta → descarga → acusa |

Al volver al primer plano, la app reconecta SignalR y **sincroniza lo que ocurrió mientras estuvo fuera**, consultando las notificaciones sin acusar.

### ⚠ `PENDIENTE-E5` — salida a internet

FCM necesita que los teléfonos alcancen los servidores de Google. **Si la Wi-Fi de planta está completamente aislada, esto no funciona** y habría que abrir salida hacia FCM o volver a una solución interna. Es la pendiente más relevante que queda, y hay que confirmarla antes de la fase F10.

---

# 3 · Seguridad y rendimiento

## 3.1 Cifrado en tránsito

| Aspecto | Requisito |
|---|---|
| Protocolo | **TLS 1.2 mínimo**, 1.3 preferido |
| HTTP plano | **Prohibido**, sin excepción para desarrollo en dispositivo |
| Fijación de certificado | **Obligatoria** contra el certificado del servidor de planta |
| Configuración de red | `cleartextTrafficPermitted="false"` + lista blanca de un solo host |
| Cifrados | Solo suites modernas con confidencialidad directa |

> **Por qué fijación de certificado:** la red Wi-Fi de planta es un entorno donde un punto de acceso no autorizado es plausible. Sin fijación, un intermediario podría interceptar **restricciones médicas de 160 personas**. Con certificado interno, además, la fijación es sencilla de operar.

## 3.2 Cifrado en reposo

### Servidor

| Dato | Protección |
|---|---|
| Base completa | **TDE** (Transparent Data Encryption) |
| Copias de seguridad | Cifradas, clave custodiada aparte |
| Contraseñas y PIN | **PBKDF2** o Argon2id con sal por usuario |
| Tokens de refresco | Almacenados solo como hash |

### Dispositivo `[SEGURIDAD DE DATOS]` *(D3)*

| Dato | Protección |
|---|---|
| Caché operativa | **Room + SQLCipher**, clave en Android Keystore |
| **Restricciones médicas** | Misma base cifrada. **Alcance: solo su línea + presentes en ella. Nunca el padrón completo** |
| Tokens | `EncryptedSharedPreferences`, respaldo desactivado |
| Registros | **Nunca** contienen nombre, ficha ni dato médico |
| Capturas | `FLAG_SECURE` en pantallas con datos médicos |

**Purga obligatoria** de la caché en: cierre de sesión, cierre de turno, reasignación de línea, inactividad configurable, y detección de dispositivo rooteado.

> **Por qué se cachea información médica pese al riesgo:** §12.1 exige que una terminal sin red *"se vea y se comporte igual que una conectada"*, y §12.2 hace de mostrar las restricciones activas un **requisito previo** a consolidar cualquier registro. Sin caché, lo primero que se rompe al perder la red es justamente la pantalla de seguridad — el peor resultado posible. Se cachea, pero con el alcance mínimo y cifrada.
>
> **El dispositivo del Coordinador no cachea restricciones médicas de las 10 líneas.** Las consulta en línea bajo demanda: su alcance es 160 personas, y precargarlo convertiría un teléfono extraviado en una fuga del padrón médico completo.

## 3.3 Autenticación y autorización *(D6)*

```
Access token   JWT · 15 min · claims: sub, rol, nombre
                   ⚠ SIN linea_id (§2.3)
Refresh token  Opaco · 12 h · ligado a device_id · hash en base
PIN            4–6 dígitos · reentrada durante el turno · 3 fallos → login completo
Identidad      AD / Entra ID si existe; respaldo local
```

**Autorización en dos dimensiones, evaluadas siempre juntas** *(véase [04 §6.2](04_ESQUEMA_BACKEND.md))*:

```
AUTORIZADO = (el rol permite la operación) Y (el alcance cubre la línea)
```

Tres capas de aplicación:
1. **Filtro de autorización** por rol en el endpoint.
2. **Filtro de alcance** en el repositorio, resolviendo la línea **en vivo** desde `Linea.supervisor_actual`.
3. **Seguridad a nivel de fila (RLS)** en SQL Server como red de seguridad.

> **La línea no viaja en el token, deliberadamente** *(§2.3)*. Si viajara, una reasignación del Coordinador tardaría hasta 15 minutos en surtir efecto, y durante ese tiempo un supervisor operaría sobre una línea que ya no es suya. Se resuelve por petición: cuesta una consulta indexada y elimina toda una clase de fallo de autorización.

## 3.4 Requisitos de rendimiento

Presupuestos, no aspiraciones. Se miden en integración continua y **fallan el build** si se superan.

| Operación | Objetivo p95 | Máximo | Por qué ese número |
|---|---|---|---|
| Resolver escaneo de gafete | **300 ms** | 500 ms | El supervisor está de pie con la persona delante |
| Validar y asignar | **500 ms** | 1 s | Incluye 7 reglas + transacción |
| Cargar malla de línea | **800 ms** | 1.5 s | Pantalla de entrada, se abre decenas de veces por turno |
| Panel de planta (10 líneas) | **1.2 s** | 2 s | Vista del Coordinador |
| Cola de relevos | 600 ms | 1 s | — |
| Propagación de evento en vivo | **< 2 s** | 5 s | C4: los dos paneles no pueden divergir de forma perceptible |
| Barrido de puestos fijos | **< 10 s** | 20 s | Momento crítico del turno |
| Arranque en frío de la app | 2 s | 3 s | — |
| Recálculo de estadística | 400 ms | 800 ms | C4 |

**Escala real:** 10 líneas · ~160 trabajadores · ~11 dispositivos concurrentes (10 supervisores + 1 Coordinador) · unos 300 puestos.

> **Es una carga muy modesta.** El riesgo de este sistema no es el volumen: es la **corrección bajo concurrencia** en momentos puntuales (el arranque, un paro que libera doce personas de golpe). Por eso el esfuerzo de ingeniería se concentra en transacciones e índices, no en escalado horizontal.

## 3.5 Endurecimiento

| Medida | Detalle |
|---|---|
| Limitación de tasa | Por usuario y por dispositivo; más estricta en `/auth` |
| Bloqueo de cuenta | Tras N intentos, con retardo creciente |
| Idempotencia | Obligatoria en toda escritura *(§12.4)* |
| Validación de entrada | En el servidor, siempre. La del cliente es solo experiencia |
| Cabeceras | HSTS, `X-Content-Type-Options`, `Referrer-Policy` |
| Secretos | Nunca en el repositorio; variables de entorno o almacén de secretos |
| Dependencias | Escaneo de vulnerabilidades en cada compilación |
| Ofuscación | R8 en modo release |
| Detección de root | Degradación del modo sin conexión: sin caché de datos médicos |

---

# 4 · Políticas de escalabilidad y arquitectura

## 4.1 Backend por capas

```
┌──────────────────────────────────────────────┐
│ API — controladores, SignalR, autorización   │
├──────────────────────────────────────────────┤
│ APLICACIÓN — casos de uso, orquestación      │
├──────────────────────────────────────────────┤
│ DOMINIO — motores, reglas, entidades         │  ← sin dependencias externas
├──────────────────────────────────────────────┤
│ INFRAESTRUCTURA — EF Core, Dapper, SPs, AD   │
└──────────────────────────────────────────────┘
```

### Los cuatro motores viven separados `[REGLA DE ARQUITECTURA]`

> **A9 lo exige explícitamente**: el motor de relevos se rige **solo** por proximidad y compatibilidad; la prioridad de líneas **solo** aplica a la asignación inicial.

```
Dominio/
  Motores/
    AsignacionInicial/       → usa PRIORIDAD (§8.3)
    Relevos/                 → usa PROXIMIDAD (§9.4, A1)  ⚠ NO conoce prioridad
    ExtraccionInversa/       → usa PRIORIDAD INVERTIDA (§9.6, A5)
    Validacion/              → las 7 reglas (§7.1)
```

> **Regla de arquitectura, verificable:** el ensamblado `Relevos` **no referencia** el servicio de prioridad. Hay una prueba de arquitectura que falla la compilación si esa dependencia aparece.
>
> **Por qué tan estricto:** implementarlos como un motor parametrizado parece elegante y es el camino directo a que la prioridad se filtre a una decisión de relevo. A9 lo prohíbe, y una regla que solo vive en un documento se rompe en el primer refactor. Escrita como prueba de arquitectura, no.

### Patrones aplicados

| Patrón | Dónde | Por qué |
|---|---|---|
| Repositorio | Acceso a datos | Aísla EF/Dapper del dominio |
| Estrategia | Escalera §8.5, ranking B2, destino B4 | Cada nivel es una estrategia; añadir uno no toca los demás |
| Cadena de responsabilidad | Validación §7.1 | **El primer rechazo detiene** — es literalmente el patrón |
| Máquina de estados | Trabajador, puesto, línea | Las transiciones ilegales no compilan |
| Especificación | Compatibilidad §4.2, médicas §7.2 | Reglas componibles y comprobables por unidad |
| Bandeja de salida | Eventos en vivo | Garantiza que el evento se emite si y solo si la transacción confirmó |

> **La bandeja de salida importa más de lo que parece para C4.** Si el evento se emitiera antes de confirmar la transacción, un fallo posterior dejaría los paneles mostrando un paro que no se registró. La bandeja garantiza que el panel y la base cuentan siempre la misma historia.

## 4.2 Android: Clean Architecture

```
app/
├── core/          diseño, red, seguridad, extensiones
├── data/          repositorios, Retrofit, Room+SQLCipher, SignalR
├── domain/        modelos, casos de uso, contratos de repositorio
└── feature/
    ├── auth/            login, PIN
    ├── linea/           malla, detalle de puesto
    ├── bolson/          pantalla exclusiva de L8 (C7)
    ├── relevo/          cola, propuesta, recepción
    ├── contingencia/    paro, cronómetro, lote
    ├── coordinador/     panel, planificación, maestros, padrón
    └── comun/           escáner, confirmación de identidad, banner offline
```

### Regla de dependencia

`feature → domain ← data`. **El dominio no conoce Android, ni Retrofit, ni Room.** La consecuencia práctica: las reglas de negocio del cliente se pueden probar con pruebas unitarias puras, sin emulador.

> **Matiz que este proyecto no puede olvidar:** el dominio del cliente **replica** algunas reglas para anticipar el resultado y dar buena experiencia, pero **nunca decide**. §7 lo dice sin ambigüedad: *"La interfaz puede anticipar el resultado para dar buena experiencia, pero la decisión final nunca puede quedar del lado del dispositivo."*
>
> **Implementación de esa regla:** toda operación de escritura se revalida en el servidor. Si el cliente anticipó "sí" y el servidor dice "no", **manda el servidor** y la interfaz muestra el rechazo explicado. La anticipación del cliente jamás es la última palabra.

## 4.3 Estrategia de modo sin conexión *(§12.1)*

> §12.1: **bloqueo defensivo, no cola optimista.**

```
┌─────────────────────────────────────────────────────┐
│  SIN CONEXIÓN                                       │
├─────────────────────────────────────────────────────┤
│  LECTURA    ✅ desde caché cifrada, con sello (D4)  │
│  ESCRITURA  ⛔ BLOQUEADA — no se encola nada        │
│  COLA       ⛔ NO EXISTE, por diseño                │
└─────────────────────────────────────────────────────┘
```

**Consecuencia arquitectónica explícita:** el cliente **no tiene** cola de operaciones pendientes, ni `WorkManager` de reintento de escrituras, ni resolución de conflictos. Es intencional y es lo contrario de la arquitectura offline habitual.

> **Por qué** *(§12.1)*: *"Un rechazo digital no deshace un traslado físico. Si el supervisor ya le dijo a alguien que camine a otra línea y la operación se rechaza al volver la red, el sistema y la realidad quedan desincronizados, y nadie se entera hasta que falta una persona."*
>
> Toda la sincronización compleja que un sistema offline normal necesita, aquí **se elimina**. La app es más simple, y es más simple porque el negocio lo exigió por una buena razón.

**Detección de conectividad:** no basta con `NetworkCapabilities` — el Wi-Fi de planta puede estar asociado sin llegar al servidor. Se usa un **latido contra el servidor** y el estado de la conexión SignalR. Estar asociado a un punto de acceso no cuenta como estar conectado.

## 4.4 Cómo crece el sistema

| Eje | Estado hoy | Si crece |
|---|---|---|
| Líneas | 10, fijas en el modelo | El esquema no las limita; la proximidad crece a N×(N−1) |
| Dispositivos | ~11 concurrentes | Una sola instancia sobra; con más, backplane de Redis para SignalR |
| Datos históricos | Indefinidos *(D7)* | Particionado por `dia_operacion` en `Movimiento` y `Auditoria` |
| Plantas | Una | Requeriría `planta_id` en todo el modelo — **fuera de alcance** |
| Turnos | Configurables | Ya soportado |

> **No se sobre-diseña para escala que nadie pidió.** Once dispositivos concurrentes no justifican microservicios, colas de mensajes ni orquestación de contenedores. Añadir esa complejidad haría el sistema más frágil, y la fragilidad aquí significa que un supervisor no puede colocar a alguien.

---

# 5 · Estructura de repositorio

```
smartassign/
├── docs/                          esta documentación
│   └── fuentes/                   especificación funcional y anexo
├── backend/
│   ├── SmartAssign.Api/           controladores, hubs, autorización
│   ├── SmartAssign.Application/   casos de uso
│   ├── SmartAssign.Domain/        motores y reglas — sin dependencias
│   ├── SmartAssign.Infrastructure/ EF Core, Dapper, SPs, AD
│   ├── SmartAssign.Migrations/    migraciones versionadas
│   └── tests/
│       ├── Domain.UnitTests/
│       ├── Api.IntegrationTests/
│       ├── Reglas.SeguridadTests/   ← suite dedicada a reglas duras
│       └── Arquitectura.Tests/      ← verifica que Relevos ignora prioridad
├── android/
│   ├── app/
│   ├── core/ data/ domain/ feature/
│   └── tests/
└── ops/
    ├── ci/                        canalizaciones
    ├── deploy/                    guiones de despliegue
    └── seed/                      datos semilla, incluida la proximidad corregida (A1)
```

---

# 6 · Estrategia de testing

## 6.1 Pirámide, con una capa añadida

```
        ╱  E2E ╲            pocas, flujos completos
       ╱────────╲
      ╱ Integr.  ╲          API + base real
     ╱────────────╲
    ╱  Unitarias   ╲        motores y reglas
   ╱────────────────╲
  ╱ REGLAS DE SEGURIDAD ╲   ← capa propia, obligatoria
 ╱──────────────────────╲
```

## 6.2 Suite de reglas de seguridad `[OBLIGATORIA]`

Es la capa que este producto necesita y que una pirámide estándar no contempla. **Si un solo caso falla, no hay despliegue.**

| Caso | Verifica |
|---|---|
| Restricción médica × 8 caminos de asignación | §7.2 no cede en ninguno |
| Compatibilidad de categoría × todos los motores | §4.2 |
| Coordinador intentando saltar médicas | §2.1.9 — **debe fallar** |
| Liderazgo manual sin justificación | A7b — debe fallar |
| Supervisor consultando otra línea × cada endpoint | §2.2 |
| Carga útil de `AvisoFatigaPlanta` | D2 — sin identidad |
| Proyección de `vw_SolicitudRelevo_L8` | D1 — sin identidad ni médicas |
| Suscripción a grupo de línea ajena | §2.2 — debe rechazarse |
| Concurrencia: dos supervisores, una persona | §7.5, B1 — exactamente un ganador |
| Atomicidad ante fallo a mitad de transacción | §7.5 — sin estado parcial |
| Escritura con red caída | §12.1 — bloqueada, **sin encolar** |
| Escritura directa en `Asignacion` con credenciales de app | [04 §7.5](04_ESQUEMA_BACKEND.md) — debe fallar |
| Regla de 24 h con tres días de descanso | B6 — sigue bloqueando |
| Reserva doble del mismo puesto | B4 — debe fallar |

## 6.3 Otras capas

**Unitarias** — cada motor por separado, con la tabla de proximidad corregida (A1), umbrales por puesto (A4), ranking B2, orden de cola B3, destino B4, extracción inversa con piso B5, guarda anti-dominó C15.

**Integración** — cada endpoint contra base real: alcance, idempotencia, transaccionalidad, formato de error con `detail` y `siguientePaso`.

**Arquitectura** — `Relevos` no referencia prioridad *(A9)*; `Domain` no referencia infraestructura; ningún host de red distinto del servidor de planta *(§12.1)*.

**Android** — casos de uso puros; interfaz con Compose Test; **la app completa en escala de grises** *(§12.2)*; zonas de toque ≥ 48 dp y acción primaria ≥ 64 dp *(A11)*; contraste medido *(§12.3)*.

**E2E** — turno completo: planificación → arranque → llenado → fatiga → relevo en cadena → paro → cierre de lote → cierre de turno.

## 6.4 Cobertura

| Área | Mínimo |
|---|---|
| `Domain/Motores` | **95 %** |
| `Domain/Validacion` | **100 %** — no admite excepción |
| Aplicación | 85 % |
| API | 80 % |
| Android `domain` | 90 % |

---

# 7 · Despliegue

## 7.1 Servidor

```
Windows Server (⚠ PENDIENTE-E3)
├── IIS o servicio de Windows
│   └── SmartAssign.Api  (Kestrel tras proxy inverso)
├── SQL Server 2019+
│   ├── TDE activo
│   └── copias: completa diaria + diferencial cada 6 h + registro cada 15 min
└── Red: solo HTTPS entrante desde la Wi-Fi de planta
```

**Entornos:** desarrollo (local) · preproducción (réplica con datos anonimizados) · producción.

**Migraciones:** solo entre turnos, con verificación previa de cero tránsitos abiertos y cero lotes abiertos *(véase [04 §11.4](04_ESQUEMA_BACKEND.md))*.

## 7.2 Distribución del APK y alta de dispositivo *(F3)*

**Autoalojada en el propio servidor de planta.** Sin MDM, sin Play Store, sin Firebase App Distribution — un sistema menos que administrar.

```
1 · PRIMERA INSTALACIÓN
    El Coordinador abre [Alta de dispositivo] y muestra un QR
    con la URL del servidor
              │
    El supervisor escanea ese QR con la app recién instalada
              │
    La app queda configurada. Cero tecleo.

2 · ACTUALIZACIONES
    Al iniciar sesión, la app consulta GET /app/version
              │
    ¿Hay versión nueva? → ofrece actualizar dentro de la app
              │
    Descarga el APK del mismo servidor e invoca al instalador

3 · CONVIVENCIA DE VERSIONES
    La API mantiene compatibilidad dentro de v1.
    NO se fuerza a que todos los dispositivos actualicen el mismo día
    (Anexo §3). Solo se bloquea si la API declara incompatibilidad dura.
```

**Un solo APK para los dos roles.** El rol sale del inicio de sesión, no de una compilación distinta *(F4)*. No hay "app de coordinador" y "app de supervisor" que mantener por separado.

| Opción descartada | Motivo |
|---|---|
| MDM / Device Owner | **Ya no hace falta**: D5 con FCM elimina la dependencia que lo justificaba |
| Firebase App Distribution | Un servicio más que administrar, sin ventaja sobre alojarlo uno mismo |
| Pista interna de Google Play | Requiere cuenta de desarrollador y proceso de publicación para una app interna de 11 dispositivos |

## 7.3 Integración y entrega continuas

```
BACKEND
  compilar → pruebas unitarias → pruebas de arquitectura
  → integración con SQL en contenedor
  → SUITE DE REGLAS DE SEGURIDAD  ⛔ bloqueante
  → análisis estático → escaneo de dependencias
  → publicar artefacto

ANDROID
  compilar → pruebas unitarias → lint
  → pruebas instrumentadas
  → pruebas de accesibilidad (toque 48 dp, contraste AAA, escala de grises)
  → ensamblar release firmado
  → publicar al canal de distribución

PRODUCCIÓN
  aprobación manual → ventana entre turnos
  → respaldo → migración → despliegue → verificación de salud
  → reversión automática si falla la verificación
```

## 7.4 Observabilidad

| Aspecto | Implementación |
|---|---|
| Registros | Serilog estructurado. **Nunca nombre, ficha ni dato médico** |
| Métricas | Latencias contra los presupuestos de §3.4 |
| Trazas | Correlación por petición, propagada al canal en vivo |
| Salud | `/health` con estado de base y de SignalR |
| Alertas técnicas | Latencia sobre presupuesto, tasa de error, tránsitos caducados, notificaciones sin acuse |
| Panel de operación | KPIs del [PRD §5](01_PRD.md) |

> **Los registros no llevan datos personales.** Es la misma regla que §12.1 aplicada a la telemetría: un archivo de registro es un lugar donde los datos médicos salen del control de acceso sin que nadie se dé cuenta. Se registra `personal_id`, nunca el nombre.

---

# 8 · Pendientes que afectan a este documento

| ID | Impacto si cambia |
|---|---|
| ⚠ **E5 — Salida a internet** | **El de mayor impacto ahora.** FCM necesita que los teléfonos alcancen los servidores de Google. Si la Wi-Fi de planta está totalmente aislada, hay que abrir salida o volver a una solución interna |
| ⚠ E3 — Servidor | Confirma el empaquetado; no cambia el stack |
| ⚠ E4 — Dispositivos | Ajusta `minSdk`. Ya no afecta a notificaciones |
| ⚠ D7 — Retención | Requiere validación legal |

**Cerradas desde la versión anterior de este documento:** E1 (QR), E2 (sin MDM), E6 (el PDF no es fuente), F1–F4 (arquitectura de despliegue).

### Dependencia fuera del software

> **Los gafetes hay que imprimirlos con su QR** *(E1)*. Es una tarea física con plazo propio que no resuelve el equipo de desarrollo y que **bloquea las pruebas de campo de la fase F4**. Especificación de impresión en [00 §E1](00_DECISIONES.md).

---

# 9 · Trazabilidad

| Decisión técnica | Origen |
|---|---|
| Android · SQL Server | Cliente, decisión fija |
| **API intermedia confirmada** | **F1** — delegada al equipo técnico y analizada |
| Un solo ejecutable, sin contenedores | F2 |
| APK autoalojado · alta por QR · un APK para dos roles | F3, F4 |
| Coordinador en teléfono | F4 |
| **FCM como campana vacía** | **D5** — decisión del cliente sobre el medio |
| Escaneo QR en dispositivo | E1, §12.1 |
| ASP.NET Core | Anexo §2 (continuidad de ecosistema), §2.1.5, C4 |
| Escritura solo por procedimientos almacenados | §7 (encabezado) |
| SignalR con grupos por alcance | §2.1.5, §2.2, C4, D2 |
| REST en vez de GraphQL | D1 (proyección fijada por servidor) |
| `detail` + `siguientePaso` obligatorios | §1.3, §12.4 |
| Idempotencia en toda escritura | §12.4 |
| ML Kit en dispositivo | §12.1 |
| FCM sin datos de negocio · contrato de carga útil verificado | §12.1, D5 |
| Sin MDM · APK autoalojado | E2, F3 |
| Fijación de certificado | §12.1 |
| Caché cifrada con alcance de línea | §12.1, §12.2, D3 |
| Coordinador sin caché médica | D3 |
| Línea fuera del token | §2.3 |
| RLS como tercera capa | §2.2 |
| Motores separados, verificado por prueba | **A9** |
| Cadena de responsabilidad en validación | §7.1 |
| Bandeja de salida transaccional | C4, §7.5 |
| Sin cola de escritura offline | §12.1 |
| Latido en vez de estado de red | §12.1 |
| Suite de reglas de seguridad bloqueante | §7.2, §2.2, §7.5 |
| Pruebas de accesibilidad en CI | §12.2, §12.3 |
| Registros sin datos personales | §12.1, §2.2 |
