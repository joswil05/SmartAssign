# SmartAssign — Esquema Backend

**Modelo de datos en SQL Server, autenticación y roles, integridad transaccional y migraciones.**
Versión 1.0 · 2026-08-09

> **Principio rector del modelo.** La especificación establece que *"la decisión final nunca puede quedar del lado del dispositivo"* (§7). Este esquema es el lugar donde esa afirmación se vuelve verificable: las reglas duras se sostienen con **restricciones de base de datos y procedimientos almacenados**, no solo con código de aplicación. Una regla que solo vive en la capa de servicio se salta con un cliente distinto; una que vive en un `CHECK` o en un índice único, no.
>
> Convenciones: tablas en `PascalCase` singular · claves `Id` · campos en `snake_case` · fechas en `DATETIME2(0)` **UTC**, siempre del servidor *(C6)* · todo importe entero en unidades base.

---

# 1 · Modelo Entidad-Relación

## 1.1 Vista general

```
        ┌──────────┐        ┌───────────┐       ┌──────────┐
        │  Linea   │───┬───►│  Puesto   │◄──────│ TipoActiv│
        └────┬─────┘   │    └─────┬─────┘       └──────────┘
             │         │          │
             │         │          │  ┌────────────────┐
   ┌─────────┴────┐    │          └─►│  Asignacion    │◄────┐
   │ Proximidad   │    │             └───────┬────────┘     │
   │   Linea      │    │                     │              │
   └──────────────┘    │             ┌───────▼────────┐     │
                       │             │  Movimiento    │     │
        ┌──────────┐   │             └────────────────┘     │
        │Prioridad │◄──┘                                    │
        │  Linea   │        ┌──────────┐                    │
        └──────────┘        │ Personal │────────────────────┘
                            └────┬─────┘
                                 │
        ┌──────────────┐         │        ┌──────────────────┐
        │ Restriccion  │◄────────┼───────►│ CapacidadFisica  │
        │   Medica     │         │        └──────────────────┘
        └──────────────┘         │                 ▲
                                 │                 │
        ┌──────────────┐         │        ┌────────┴─────────┐
        │  Usuario     │◄────────┘        │ PuestoCapacidad  │
        └──────┬───────┘                  └──────────────────┘
               │
        ┌──────▼───────┐  ┌────────┐  ┌───────┐  ┌──────────┐
        │  Auditoria   │  │ Turno  │  │ Lote  │  │   SKU    │
        └──────────────┘  └────────┘  └───┬───┘  └──────────┘
                                          │
              ┌───────────┬───────────────┼──────────────┐
              ▼           ▼               ▼              ▼
         ┌────────┐  ┌──────────┐  ┌───────────┐  ┌────────────┐
         │  Paro  │  │Desperdic.│  │ Produccion│  │ Eficiencia │
         └────────┘  └──────────┘  └───────────┘  └────────────┘
```

---

# 2 · Catálogos y estructura de planta

## 2.1 Linea

```sql
CREATE TABLE Linea (
    Id                  TINYINT       NOT NULL PRIMARY KEY,        -- 1..10
    codigo              VARCHAR(4)    NOT NULL UNIQUE,             -- 'L1'..'L10'
    nombre              NVARCHAR(60)  NOT NULL,
    es_bolson           BIT           NOT NULL DEFAULT 0,          -- solo L8 (§3.2, C7)
    minimo_operarios    SMALLINT      NULL,                        -- NULL => default de planta (B5)
    activa_hoy          BIT           NOT NULL DEFAULT 0,
    situacion           VARCHAR(20)   NOT NULL DEFAULT 'inactiva',
    supervisor_actual   INT           NULL REFERENCES Usuario(Id),
    creado_en           DATETIME2(0)  NOT NULL DEFAULT SYSUTCDATETIME(),
    row_version         ROWVERSION    NOT NULL,

    CONSTRAINT CK_Linea_situacion CHECK (situacion IN
        ('inactiva','activa','en_arranque','en_produccion','en_paro','en_limpieza')),
    CONSTRAINT CK_Linea_minimo CHECK (minimo_operarios IS NULL OR minimo_operarios >= 0)
);

-- Solo puede existir un Bolsón en toda la planta (§3.2)
CREATE UNIQUE INDEX UX_Linea_bolson ON Linea(es_bolson) WHERE es_bolson = 1;
-- Un supervisor no puede tener dos líneas (§2.3)
CREATE UNIQUE INDEX UX_Linea_supervisor ON Linea(supervisor_actual)
    WHERE supervisor_actual IS NOT NULL;
```

> `UX_Linea_supervisor` es la **regla de supervisor único del §2.3 escrita en la base**. Es lo que impide que un error de servicio produzca dos líneas para la misma persona.

## 2.2 PrioridadLinea — configurable en caliente *(§3.3, §12.6, B8)*

```sql
CREATE TABLE PrioridadLinea (
    Id              INT IDENTITY PRIMARY KEY,
    linea_id        TINYINT      NOT NULL REFERENCES Linea(Id),
    orden           TINYINT      NOT NULL,          -- 1 = máxima prioridad
    vigente_desde   DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    vigente_hasta   DATETIME2(0) NULL,              -- NULL = vigente
    cambiado_por    INT          NOT NULL REFERENCES Usuario(Id),

    CONSTRAINT CK_Prioridad_orden CHECK (orden BETWEEN 1 AND 10)
);

CREATE UNIQUE INDEX UX_Prioridad_vigente ON PrioridadLinea(linea_id)
    WHERE vigente_hasta IS NULL;
CREATE UNIQUE INDEX UX_Prioridad_orden_vigente ON PrioridadLinea(orden)
    WHERE vigente_hasta IS NULL;
```

> **Versionado, no sobrescritura** *(B8)*: cambiar la prioridad **cierra** la fila vigente y abre otra. Así el cambio "solo hacia adelante" es demostrable y auditable, y el histórico explica por qué el barrido de ayer repartió como repartió.

**Orden base sembrado** *(§3.3)*: `L4 > L1 > L2 > L6 > L7 > L5 > L3 > L8 > L9 > L10`.

**Extracción inversa** *(A5)*: se **deriva** invirtiendo el orden vigente, excluyendo la L8 y la línea solicitante. No se almacena como segunda lista.

## 2.3 ProximidadLinea — grafo dirigido *(§9.5, A1, A2, A3)*

```sql
CREATE TABLE ProximidadLinea (
    linea_origen    TINYINT  NOT NULL REFERENCES Linea(Id),
    linea_destino   TINYINT  NOT NULL REFERENCES Linea(Id),
    orden           TINYINT  NOT NULL,          -- 1 = más cercana

    CONSTRAINT PK_Proximidad PRIMARY KEY (linea_origen, orden),
    CONSTRAINT UQ_Proximidad UNIQUE (linea_origen, linea_destino),
    CONSTRAINT CK_Proximidad_distinta CHECK (linea_origen <> linea_destino),
    CONSTRAINT CK_Proximidad_orden CHECK (orden BETWEEN 1 AND 9)
);
```

> **Es un grafo dirigido y asimétrico a propósito** *(A3)*. `PK(linea_origen, orden)` permite que L5→L1 tenga orden 1 mientras L1→L5 tiene orden 8, sin que la base lo trate como incoherencia. **Prohibido derivarlo de coordenadas o de una matriz simétrica.**
>
> La **L8 no tiene filas como origen** *(§9.5)*: nunca busca "la línea más cercana".

**Datos sembrados** — tabla vigente con la corrección de A1:

| Origen | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 |
|---|---|---|---|---|---|---|---|---|---|
| L1 | L2 | L4 | L9 | L10 | L6 | L3 | L7 | L5 | L8 |
| L2 | L4 | L1 | L7 | L9 | L10 | L3 | L6 | L5 | L8 |
| L3 | L10 | L9 | L6 | L7 | L4 | L2 | L1 | L5 | L8 |
| L4 | L2 | L1 | L7 | L9 | L10 | L6 | L3 | L5 | L8 |
| L5 | L1 | L2 | L4 | L7 | L9 | L10 | L6 | L3 | L8 |
| L6 | L3 | L10 | L9 | L7 | L4 | L2 | L1 | L5 | L8 |
| L7 | L9 | L10 | L6 | L3 | L4 | L2 | L1 | L5 | L8 |
| L8 | — sin filas — | | | | | | | | |
| L9 | L3 | L10 | L6 | L7 | L4 | L2 | L1 | L5 | L8 |
| **L10** | **L9** | **L3** | **L6** | **L7** | **L4** | **L2** | **L1** | **L5** | **L8** |

## 2.4 TipoActividad *(A4)*

```sql
CREATE TABLE TipoActividad (
    Id                          SMALLINT IDENTITY PRIMARY KEY,
    nombre                      NVARCHAR(80) NOT NULL UNIQUE,   -- ej. 'Girar botellas'
    aplica_no_repeticion_24h    BIT NOT NULL DEFAULT 0,         -- (§7.4, A4)
    activo                      BIT NOT NULL DEFAULT 1
);
```

> **La regla de 24 horas es un dato, no código** *(A4)*. Hoy solo *"Girar botellas"* lleva la bandera. Añadir otra actividad mañana es un `UPDATE`, no un despliegue.

## 2.5 SKU y PuestoSKU

```sql
CREATE TABLE SKU (
    Id                  INT IDENTITY PRIMARY KEY,
    codigo              VARCHAR(30) NOT NULL UNIQUE,
    descripcion         NVARCHAR(150) NOT NULL,
    ritmo_teorico_hora  DECIMAL(10,2) NOT NULL,     -- unidades/hora (§11.4, §12.6)
    activo              BIT NOT NULL DEFAULT 1,

    CONSTRAINT CK_SKU_ritmo CHECK (ritmo_teorico_hora > 0)
);

-- Qué puestos requiere cada SKU (§11.2)
CREATE TABLE PuestoSKU (
    puesto_id  INT NOT NULL REFERENCES Puesto(Id),
    sku_id     INT NOT NULL REFERENCES SKU(Id),
    CONSTRAINT PK_PuestoSKU PRIMARY KEY (puesto_id, sku_id)
);
```

> **`ritmo_teorico_hora` nunca es un valor fijo en código** *(§11.4)*: *"El ritmo teórico depende del SKU y proviene del catálogo."*
>
> `PuestoSKU` es lo que hace computable *"fuera de operación"* (§5.3): un puesto está fuera de operación si su línea no está activa **o** si no tiene fila para el SKU del lote vigente.

## 2.6 Puesto

```sql
CREATE TABLE Puesto (
    Id                  INT IDENTITY PRIMARY KEY,
    linea_id            TINYINT      NOT NULL REFERENCES Linea(Id),
    codigo              VARCHAR(20)  NOT NULL,
    tipo                VARCHAR(10)  NOT NULL,     -- 'fijo' | 'rotativo' (Parte V)
    tipo_actividad_id   SMALLINT     NULL REFERENCES TipoActividad(Id),
    categoria_titular   VARCHAR(15)  NULL,         -- solo fijos: 'operador_a'|'operador_c'|'averiero'
    titular_id          INT          NULL REFERENCES Personal(Id),   -- (C12)
    perfil_preferente   NVARCHAR(60) NULL,         -- regla blanda (§7.3)

    -- Umbrales de fatiga PROPIOS del puesto (A4). NULL => default de planta
    umbral_sugerido_min SMALLINT     NULL,
    umbral_critico_min  SMALLINT     NULL,

    activo              BIT          NOT NULL DEFAULT 1,
    row_version         ROWVERSION   NOT NULL,

    CONSTRAINT UQ_Puesto UNIQUE (linea_id, codigo),
    CONSTRAINT CK_Puesto_tipo CHECK (tipo IN ('fijo','rotativo')),
    CONSTRAINT CK_Puesto_categoria CHECK (
        (tipo = 'fijo' AND categoria_titular IS NOT NULL) OR
        (tipo = 'rotativo' AND categoria_titular IS NULL)),
    CONSTRAINT CK_Puesto_umbrales CHECK (
        umbral_sugerido_min IS NULL OR umbral_critico_min IS NULL
        OR umbral_critico_min > umbral_sugerido_min)
);
```

> **`titular_id` con doble semántica** *(C12)*:
> - En puestos **fijos** es asignación técnica: dispara el barrido automático del §8.3.
> - En puestos **rotativos** es **mera preferencia**: alimenta la escalera del §8.5 nivel 1 y **nunca** genera asignación automática.
>
> La distinción no vive en la columna sino en el procedimiento: `sp_BarridoPuestosFijos` filtra `tipo = 'fijo'` explícitamente. Sin ese filtro, el barrido llenaría los rotativos y §5.2 exige que empiecen vacíos.
>
> **Umbrales por puesto** *(A4)*: se siembran en `NULL` y el motor cae al parámetro de planta. Los valores reales se calibran con datos de operación.

## 2.7 Capacidades físicas — el vocabulario de la regla médica

```sql
CREATE TABLE CapacidadFisica (
    Id      SMALLINT IDENTITY PRIMARY KEY,
    codigo  VARCHAR(40) NOT NULL UNIQUE,   -- 'levantar_carga', 'bipedestacion_prolongada'
    nombre  NVARCHAR(120) NOT NULL,
    activo  BIT NOT NULL DEFAULT 1
);

-- Qué capacidades EXIGE cada puesto (§7.2)
CREATE TABLE PuestoCapacidad (
    puesto_id    INT      NOT NULL REFERENCES Puesto(Id),
    capacidad_id SMALLINT NOT NULL REFERENCES CapacidadFisica(Id),
    CONSTRAINT PK_PuestoCapacidad PRIMARY KEY (puesto_id, capacidad_id)
);
```

> **Este es el mecanismo completo de la regla médica** *(§7.2)*: *"Cada persona tiene registradas las capacidades físicas que tiene prohibidas. Cada puesto declara las capacidades que exige. Si hay coincidencia, la asignación se deniega."*
>
> Un vocabulario compartido es lo que hace posible que la verificación sea **general** — "sobre todas las restricciones registradas", no limitada a un tipo concreto de esfuerzo. Si las restricciones fueran texto libre, la comparación sería imposible de garantizar.

---

# 3 · Personal

## 3.1 Personal

```sql
CREATE TABLE Personal (
    Id                  INT IDENTITY PRIMARY KEY,
    ficha               VARCHAR(20)  NOT NULL UNIQUE,   -- número del gafete (§12.2)
    nombre_completo     NVARCHAR(150) NOT NULL,
    categoria           VARCHAR(20)  NOT NULL,          -- Parte IV
    linea_habitual      TINYINT      NULL REFERENCES Linea(Id),   -- (§8.2, C3)
    linea_fisica_actual TINYINT      NULL REFERENCES Linea(Id),   -- (§8.2)
    situacion           VARCHAR(25)  NOT NULL DEFAULT 'fuera_de_turno',  -- Parte VI
    doble_turno         BIT          NOT NULL DEFAULT 0,          -- (§11.5, B7)
    perfil              NVARCHAR(60) NULL,              -- puede ser NULL: §7.3 no infiere
    activo              BIT          NOT NULL DEFAULT 1,          -- baja/reactivación
    row_version         ROWVERSION   NOT NULL,

    CONSTRAINT CK_Personal_categoria CHECK (categoria IN
        ('operario','operador_a','operador_b','operador_c','averiero','liderazgo')),
    CONSTRAINT CK_Personal_situacion CHECK (situacion IN
        ('fuera_de_turno','presente_sin_asignar','asignado','en_transito',
         'en_bolson','retirado_temporal','ausente_justificado'))
);

CREATE INDEX IX_Personal_disponible ON Personal(situacion, linea_fisica_actual)
    INCLUDE (ficha, nombre_completo, categoria) WHERE activo = 1;
```

> **`perfil` es nulable a propósito** *(§7.3)*: *"Si el dato de la persona no está registrado, la regla no se aplica. Nunca se infiere ni se deduce."* Un `NULL` aquí significa "no evaluar", nunca "no cumple".
>
> **`linea_habitual`** es lo que resuelve el origen de la línea física al arrancar *(§8.2, C3)*, sin necesidad de integración con reloj checador.

## 3.2 RestriccionMedica `[SEGURIDAD DE DATOS]` *(§7.2, C14)*

```sql
CREATE TABLE RestriccionMedica (
    Id             INT IDENTITY PRIMARY KEY,
    personal_id    INT      NOT NULL REFERENCES Personal(Id),
    capacidad_id   SMALLINT NOT NULL REFERENCES CapacidadFisica(Id),
    fecha_inicio   DATE     NOT NULL,
    fecha_fin      DATE     NULL,             -- NULL = permanente (C14)
    fuente         NVARCHAR(120) NOT NULL,    -- quién la dictó en Enfermería
    fecha_dictamen DATE     NOT NULL,
    observacion    NVARCHAR(400) NULL,
    registrado_por INT      NOT NULL REFERENCES Usuario(Id),
    registrado_en  DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_RM_vigencia CHECK (fecha_fin IS NULL OR fecha_fin >= fecha_inicio)
);

CREATE INDEX IX_RM_vigentes ON RestriccionMedica(personal_id, capacidad_id)
    INCLUDE (fecha_inicio, fecha_fin);
```

> **Nunca se borra** *(C14)*: una restricción que deja de aplicar se **cierra** con `fecha_fin`. Borrar historial médico rompe la auditabilidad de una regla de seguridad ocupacional (§12.7).
>
> **`fuente` y `fecha_dictamen`** existen porque Enfermería no es usuario del sistema: el Coordinador transcribe (§2.1.6), y el origen en papel debe quedar rastreable.
>
> **`DELETE` está denegado sobre esta tabla para todos los roles de aplicación.** Ver §7.3.

## 3.3 AusenciaJustificada

```sql
CREATE TABLE AusenciaJustificada (
    Id            INT IDENTITY PRIMARY KEY,
    personal_id   INT  NOT NULL REFERENCES Personal(Id),
    tipo          VARCHAR(30) NOT NULL,
    fecha_inicio  DATE NOT NULL,
    fecha_fin     DATE NULL,
    registrado_por INT NOT NULL REFERENCES Usuario(Id),

    CONSTRAINT CK_Ausencia_tipo CHECK (tipo IN
        ('vacaciones','permiso','cita_medica','subsidio','accidente_laboral','otro'))
);
```

> Alimenta el estado `ausente_justificado`, que es **`[REGLA DURA]`**: *"Quien está ausente justificado NUNCA puede ser asignado. Sin excepciones"* (§6.1).

## 3.4 UltimaTareaJornada *(§7.4, A4, B6)*

```sql
CREATE TABLE UltimaTareaJornada (
    personal_id        INT      NOT NULL PRIMARY KEY REFERENCES Personal(Id),
    tipo_actividad_id  SMALLINT NOT NULL REFERENCES TipoActividad(Id),
    puesto_id          INT      NOT NULL REFERENCES Puesto(Id),
    dia_operacion      DATE     NOT NULL,      -- (C6)
    registrado_en      DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
```

> Se escribe **al cierre de turno** *(C13)*, con el **último** puesto ocupado de cada persona — no con todos los del día. Es la referencia exacta que §7.4 describe: *"la que esa persona hizo al cerrar su jornada anterior"*.
>
> `dia_operacion` es la fecha de inicio del turno *(C6)*, no la fecha calendario: los turnos que cruzan medianoche pertenecen enteros a su día de arranque.

---

# 4 · Turnos, lotes y operación

## 4.1 Turno y JornadaLinea

```sql
CREATE TABLE Turno (
    Id               TINYINT IDENTITY PRIMARY KEY,
    nombre           NVARCHAR(30) NOT NULL UNIQUE,
    hora_inicio      TIME NOT NULL,
    hora_fin         TIME NOT NULL,
    cruza_medianoche AS (CASE WHEN hora_fin <= hora_inicio THEN 1 ELSE 0 END) PERSISTED,
    activo           BIT NOT NULL DEFAULT 1
);

CREATE TABLE JornadaLinea (
    Id                 INT IDENTITY PRIMARY KEY,
    linea_id           TINYINT NOT NULL REFERENCES Linea(Id),
    turno_id           TINYINT NOT NULL REFERENCES Turno(Id),
    dia_operacion      DATE    NOT NULL,                    -- (C6)
    sku_id             INT     NULL REFERENCES SKU(Id),     -- NULL => línea inactiva (§8.1)
    supervisor_id      INT     NULL REFERENCES Usuario(Id),
    estado             VARCHAR(20) NOT NULL DEFAULT 'planificada',
    arrancado_en       DATETIME2(0) NULL,
    ventana_arranque_fin DATETIME2(0) NULL,                 -- (§8.4)
    cerrado_en         DATETIME2(0) NULL,
    cerrado_forzado_por INT    NULL REFERENCES Usuario(Id), -- (C13, A6)
    row_version        ROWVERSION NOT NULL,

    CONSTRAINT UQ_Jornada UNIQUE (linea_id, turno_id, dia_operacion),
    CONSTRAINT CK_Jornada_estado CHECK (estado IN
        ('planificada','confirmada','arrancada','cerrada'))
);
```

> **`ventana_arranque_fin` es por jornada-línea, no global.** La ventana se calcula al arrancar sumando el parámetro configurable, y el servidor es la única autoridad sobre si sigue abierta *(§8.4, §7)*.

## 4.2 Lote *(C5)*

```sql
CREATE TABLE Lote (
    Id                INT IDENTITY PRIMARY KEY,
    jornada_linea_id  INT NOT NULL REFERENCES JornadaLinea(Id),
    sku_id            INT NOT NULL REFERENCES SKU(Id),
    numero            SMALLINT NOT NULL,        -- 1, 2, 3... dentro de la jornada
    abierto_en        DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    cerrado_en        DATETIME2(0) NULL,
    produccion_real   DECIMAL(12,2) NULL,       -- (C4)
    row_version       ROWVERSION NOT NULL,

    CONSTRAINT UQ_Lote UNIQUE (jornada_linea_id, numero)
);

-- Solo un lote abierto por jornada-línea a la vez (C5)
CREATE UNIQUE INDEX UX_Lote_abierto ON Lote(jornada_linea_id) WHERE cerrado_en IS NULL;
```

## 4.3 Producción, paros y desperdicio

```sql
-- Avances parciales durante el lote (C4)
CREATE TABLE ProduccionAvance (
    Id            INT IDENTITY PRIMARY KEY,
    lote_id       INT NOT NULL REFERENCES Lote(Id),
    cantidad      DECIMAL(12,2) NOT NULL,
    registrado_por INT NOT NULL REFERENCES Usuario(Id),
    registrado_en DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT CK_Avance_cantidad CHECK (cantidad >= 0)
);

CREATE TABLE CategoriaParo (
    Id     SMALLINT IDENTITY PRIMARY KEY,
    nombre NVARCHAR(60) NOT NULL UNIQUE,   -- mecánico, eléctrico, calidad, falta de material
    activo BIT NOT NULL DEFAULT 1
);

CREATE TABLE CausaParo (
    Id           SMALLINT IDENTITY PRIMARY KEY,
    categoria_id SMALLINT NOT NULL REFERENCES CategoriaParo(Id),
    nombre       NVARCHAR(100) NOT NULL,
    activo       BIT NOT NULL DEFAULT 1,
    CONSTRAINT UQ_Causa UNIQUE (categoria_id, nombre)
);

CREATE TABLE Paro (
    Id               INT IDENTITY PRIMARY KEY,
    jornada_linea_id INT NOT NULL REFERENCES JornadaLinea(Id),
    lote_id          INT NULL REFERENCES Lote(Id),
    categoria_id     SMALLINT NOT NULL REFERENCES CategoriaParo(Id),
    causa_id         SMALLINT NOT NULL REFERENCES CausaParo(Id),
    descripcion      NVARCHAR(500) NOT NULL,          -- OBLIGATORIA (§11.1)
    inicio           DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    fin              DATETIME2(0) NULL,
    registrado_por   INT NOT NULL REFERENCES Usuario(Id),
    reanudado_por    INT NULL REFERENCES Usuario(Id),

    CONSTRAINT CK_Paro_descripcion CHECK (LEN(LTRIM(RTRIM(descripcion))) > 0),
    CONSTRAINT CK_Paro_fin CHECK (fin IS NULL OR fin >= inicio)
);

CREATE UNIQUE INDEX UX_Paro_abierto ON Paro(jornada_linea_id) WHERE fin IS NULL;

CREATE TABLE Desperdicio (
    Id                    INT IDENTITY PRIMARY KEY,
    lote_id               INT NOT NULL REFERENCES Lote(Id),
    dano_origen           DECIMAL(12,2) NOT NULL DEFAULT 0,   -- (§11.3)
    dano_proceso          DECIMAL(12,2) NOT NULL DEFAULT 0,
    justificacion         NVARCHAR(600) NULL,                 -- exigida sobre umbral (§11.3)
    registrado_por        INT NOT NULL REFERENCES Usuario(Id),
    registrado_en         DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_Desp_valores CHECK (dano_origen >= 0 AND dano_proceso >= 0)
);
```

> **`CK_Paro_descripcion`** convierte en restricción de base lo que §11.1 exige: *"El supervisor debe escribir qué observó antes de confirmar. Es lo que convierte el registro en información útil para mantenimiento."*
>
> **`UX_Paro_abierto`** impide dos paros simultáneos en la misma línea, que haría incalculable el tiempo efectivo de marcha del §11.4.
>
> El umbral que exige justificación es un parámetro *(§12.6)*; la validación de que la justificación existe cuando se supera vive en `sp_CerrarLote`.

---

# 5 · Asignaciones y movimientos

## 5.1 Asignacion — el corazón del modelo

```sql
CREATE TABLE Asignacion (
    Id                  BIGINT IDENTITY PRIMARY KEY,
    jornada_linea_id    INT NOT NULL REFERENCES JornadaLinea(Id),
    puesto_id           INT NOT NULL REFERENCES Puesto(Id),
    personal_id         INT NOT NULL REFERENCES Personal(Id),
    titular_original_id INT NULL REFERENCES Personal(Id),   -- (§8.3)
    origen              VARCHAR(25) NOT NULL,
    inicio              DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    fin                 DATETIME2(0) NULL,
    motivo_fin          VARCHAR(30) NULL,
    asignado_por        INT NOT NULL REFERENCES Usuario(Id),
    justificacion_id    BIGINT NULL REFERENCES JustificacionExcepcion(Id),
    cede_perfil         BIT NOT NULL DEFAULT 0,             -- (§8.5 niveles 2 y 4)
    row_version         ROWVERSION NOT NULL,

    CONSTRAINT CK_Asig_origen CHECK (origen IN
        ('barrido_automatico','manual_supervisor','relevo','reasignacion_relevado',
         'extraccion_inversa','intervencion_coordinador','cobertura_vacante_critica')),
    CONSTRAINT CK_Asig_fin CHECK (fin IS NULL OR fin >= inicio)
);

-- ═══ REGLAS DURAS DE INTEGRIDAD ═══
-- Un puesto no puede tener dos asignaciones activas
CREATE UNIQUE INDEX UX_Asig_puesto_activo ON Asignacion(puesto_id) WHERE fin IS NULL;
-- Una persona no puede estar asignada en dos sitios (§7.5, B1)
CREATE UNIQUE INDEX UX_Asig_personal_activo ON Asignacion(personal_id) WHERE fin IS NULL;

CREATE INDEX IX_Asig_fatiga ON Asignacion(jornada_linea_id, inicio)
    INCLUDE (puesto_id, personal_id) WHERE fin IS NULL;
```

> **Los dos índices únicos filtrados son la implementación literal del §7.5.** *"Nunca puede ocurrir que ambos crean que la tienen"* deja de ser una aspiración: es una violación de índice que la base rechaza aunque el código de aplicación fallara.
>
> **`titular_original_id`** implementa §8.3: *"cuando reaparece —vuelve de enfermería, llega tarde— el sistema debe saber cuál era su máquina para devolvérsela. Si al asignar al suplente se pierde ese dato, la información desaparece."* Es lo que hace posible el flujo C1.
>
> **La fatiga se calcula desde `inicio` de la asignación activa**, no desde un campo del trabajador. Por eso *"la fatiga es propiedad del puesto ocupado, no de la categoría de la persona"* (A7) es cierto por construcción.

## 5.2 Movimiento — despacho, tránsito y recepción *(Parte X, §12.7)*

```sql
CREATE TABLE Movimiento (
    Id                  BIGINT IDENTITY PRIMARY KEY,
    personal_id         INT NOT NULL REFERENCES Personal(Id),
    linea_origen        TINYINT NOT NULL REFERENCES Linea(Id),
    linea_destino       TINYINT NOT NULL REFERENCES Linea(Id),
    puesto_destino_id   INT NULL REFERENCES Puesto(Id),      -- reservado (§9.4 p3)
    motivo              VARCHAR(30) NOT NULL,
    estado              VARCHAR(20) NOT NULL DEFAULT 'en_transito',

    -- §12.7: hora exacta de SALIDA y de LLEGADA de cada movimiento
    hora_salida         DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    hora_llegada        DATETIME2(0) NULL,
    duracion_seg        AS (DATEDIFF(SECOND, hora_salida, hora_llegada)) PERSISTED,

    despachado_por      INT NOT NULL REFERENCES Usuario(Id),
    recibido_por        INT NULL REFERENCES Usuario(Id),
    motivo_rechazo_id   SMALLINT NULL REFERENCES MotivoRechazoRecepcion(Id),  -- (C10)
    nota_rechazo        NVARCHAR(300) NULL,
    caducado_en         DATETIME2(0) NULL,                   -- (B11)
    cancelado_por       INT NULL REFERENCES Usuario(Id),
    justificacion_id    BIGINT NULL REFERENCES JustificacionExcepcion(Id),

    CONSTRAINT CK_Mov_estado CHECK (estado IN
        ('en_transito','recibido','rechazado','cancelado')),
    CONSTRAINT CK_Mov_motivo CHECK (motivo IN
        ('relevo','reasignacion_relevado','liberacion_bolson','paro',
         'cambio_sku','linea_inactiva','rechazo_recepcion',
         'intervencion_coordinador','cobertura_vacante_critica')),
    CONSTRAINT CK_Mov_rechazo CHECK (
        estado <> 'rechazado' OR motivo_rechazo_id IS NOT NULL)   -- (C10)
);

-- Una persona no puede estar en dos tránsitos a la vez: inmunidad (§6.1)
CREATE UNIQUE INDEX UX_Mov_transito ON Movimiento(personal_id) WHERE estado = 'en_transito';
-- Un puesto no puede estar reservado para dos personas (B4, guarda anti-convergencia)
CREATE UNIQUE INDEX UX_Mov_reserva ON Movimiento(puesto_destino_id)
    WHERE estado = 'en_transito' AND puesto_destino_id IS NOT NULL;

CREATE INDEX IX_Mov_analitica ON Movimiento(linea_origen, linea_destino)
    INCLUDE (duracion_seg) WHERE estado = 'recibido';
```

> **`duracion_seg` es una columna calculada persistida y es el motivo por el que §12.7 existe.** La especificación lo dice sin ambigüedad: *"es información que hoy no decide nada en el momento, pero es la materia prima del análisis posterior: cuánto tarda realmente un traslado entre cada par de líneas."* `IX_Mov_analitica` hace esa consulta trivial y es lo que permitirá calibrar `duracion_maxima_transito` (B11) y validar la jerarquía de proximidad (A1) con datos reales en vez de a ciegas.
>
> **`UX_Mov_transito`** es la inmunidad del §6.1 escrita en la base.
> **`UX_Mov_reserva`** es la guarda de B4: dos relevistas no pueden converger al mismo puesto.
>
> **`CK_Mov_rechazo`** obliga a que todo rechazo de recepción lleve motivo *(C10)*: sin él, rechazar se convierte en un canal silencioso para esquivar relevos.

## 5.3 Relevo y descartados

```sql
CREATE TABLE SolicitudRelevo (
    Id               BIGINT IDENTITY PRIMARY KEY,
    puesto_id        INT NOT NULL REFERENCES Puesto(Id),
    jornada_linea_id INT NOT NULL REFERENCES JornadaLinea(Id),
    origen           VARCHAR(25) NOT NULL,
    nivel            VARCHAR(12) NOT NULL,
    exceso_relativo  DECIMAL(6,2) NULL,      -- % sobre umbral propio (A4, B3)
    creada_en        DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    resuelta_en      DATETIME2(0) NULL,
    resultado        VARCHAR(20) NULL,
    movimiento_id    BIGINT NULL REFERENCES Movimiento(Id),

    CONSTRAINT CK_SR_origen CHECK (origen IN
        ('umbral_automatico','manual_supervisor','vacante_critica')),  -- (C15-N1)
    CONSTRAINT CK_SR_nivel CHECK (nivel IN ('sugerido','critico','maxima')),
    CONSTRAINT CK_SR_resultado CHECK (resultado IS NULL OR resultado IN
        ('cubierta','cancelada','cierre_turno'))
);

CREATE UNIQUE INDEX UX_SR_abierta ON SolicitudRelevo(puesto_id) WHERE resuelta_en IS NULL;

CREATE TABLE RelevoDescartado (
    Id            BIGINT IDENTITY PRIMARY KEY,
    puesto_id     INT NOT NULL REFERENCES Puesto(Id),
    personal_id   INT NOT NULL REFERENCES Personal(Id),
    jornada_dia   DATE NOT NULL,                       -- caduca por turno (B10)
    descartado_por INT NOT NULL REFERENCES Usuario(Id),
    descartado_en DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    limpiado_en   DATETIME2(0) NULL,
    limpiado_por  INT NULL REFERENCES Usuario(Id),

    CONSTRAINT UQ_Descartado UNIQUE (puesto_id, personal_id, jornada_dia)
);
```

> **`RelevoDescartado` es del par (puesto, persona)**, nunca de la persona en general *(B10)*. `jornada_dia` implementa la caducidad automática al cierre de turno, que es lo que impide el *"veto permanente e invisible"* contra el que advierte §9.4. Los descartes no se borran: se cierran con `limpiado_en`, para que quede constancia de que existió el veto.

## 5.4 JustificacionExcepcion *(A6)*

```sql
CREATE TABLE MotivoExcepcion (
    Id     SMALLINT IDENTITY PRIMARY KEY,
    nombre NVARCHAR(100) NOT NULL UNIQUE,
    activo BIT NOT NULL DEFAULT 1
);

CREATE TABLE JustificacionExcepcion (
    Id             BIGINT IDENTITY PRIMARY KEY,
    tipo_excepcion VARCHAR(35) NOT NULL,
    motivo_id      SMALLINT NOT NULL REFERENCES MotivoExcepcion(Id),
    texto          NVARCHAR(600) NOT NULL,        -- OBLIGATORIO
    usuario_id     INT NOT NULL REFERENCES Usuario(Id),
    creada_en      DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),

    CONSTRAINT CK_JE_texto CHECK (LEN(LTRIM(RTRIM(texto))) >= 10),
    CONSTRAINT CK_JE_tipo CHECK (tipo_excepcion IN
        ('movimiento_fuera_de_flujo','saltar_ventana_arranque','forzar_cierre_turno',
         'extraccion_operador_b','forzar_bajo_piso_seguridad','cancelar_transito',
         'asignacion_liderazgo'))
);
```

> **`CK_JE_texto` con longitud mínima** es deliberado: un formulario obligatorio que se puede rellenar con un espacio no es un formulario obligatorio. §2.1.9 describe una excepción que responde a *"permisos o acuerdos hablados directamente entre el trabajador y el Coordinador"*, y ese contexto necesita quedar escrito para ser auditable.

---

# 6 · Autenticación, roles y aislamiento

## 6.1 Usuario y sesión *(D6)*

```sql
CREATE TABLE Usuario (
    Id                INT IDENTITY PRIMARY KEY,
    username          NVARCHAR(80) NOT NULL UNIQUE,
    nombre_completo   NVARCHAR(150) NOT NULL,
    rol               VARCHAR(15) NOT NULL,
    origen_identidad  VARCHAR(10) NOT NULL DEFAULT 'local',   -- 'ad' | 'local'
    password_hash     VARBINARY(256) NULL,        -- solo si origen = 'local'
    password_salt     VARBINARY(64)  NULL,
    pin_hash          VARBINARY(256) NULL,        -- PIN de reentrada (D6)
    pin_salt          VARBINARY(64)  NULL,
    personal_id       INT NULL REFERENCES Personal(Id),
    activo            BIT NOT NULL DEFAULT 1,
    bloqueado_hasta   DATETIME2(0) NULL,
    intentos_fallidos TINYINT NOT NULL DEFAULT 0,

    CONSTRAINT CK_Usuario_rol CHECK (rol IN ('coordinador','supervisor')),
    CONSTRAINT CK_Usuario_origen CHECK (origen_identidad IN ('ad','local'))
);

CREATE TABLE SesionDispositivo (
    Id                 UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    usuario_id         INT NOT NULL REFERENCES Usuario(Id),
    device_id          NVARCHAR(120) NOT NULL,
    refresh_token_hash VARBINARY(256) NOT NULL,
    emitido_en         DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    expira_en          DATETIME2(0) NOT NULL,
    revocado_en        DATETIME2(0) NULL,
    ultima_actividad   DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_Sesion_activa ON SesionDispositivo(usuario_id, device_id)
    WHERE revocado_en IS NULL;
```

> **Exactamente dos roles** *(Parte II)*. El supervisor de la L8 **no es un tercer rol**: es un supervisor cuya línea asignada es la que tiene `es_bolson = 1`. Sus capacidades adicionales se derivan de ese hecho, no de un permiso distinto. Modelarlo como tercer rol duplicaría reglas y abriría la puerta a que un supervisor normal heredara permisos de L8 por error de configuración.
>
> **`personal_id` nulable** vincula al usuario con su ficha cuando además es personal de planta (§4.1, liderazgo).
>
> **La línea NO se guarda en la sesión ni en el dispositivo.** Se resuelve en cada petición desde `Linea.supervisor_actual`, porque §2.3 exige que la determine el sistema. Guardarla en el token permitiría que una reasignación del Coordinador tardara hasta 15 minutos en surtir efecto.

## 6.2 Modelo de autorización (RBAC + alcance por línea)

La autorización tiene **dos dimensiones que se evalúan siempre juntas**:

```
AUTORIZADO  =  (el rol permite la operación)  Y  (el alcance cubre la línea)
```

| Rol | Alcance | Cómo se resuelve |
|---|---|---|
| **Coordinador** | Las 10 líneas | Sin filtro de línea |
| **Supervisor** | **Solo su línea** | `Linea.supervisor_actual = @usuario_id` en cada consulta |
| **Supervisor de L8** | Su línea + **proyección restringida** de puestos ajenos con solicitud abierta | Vista `vw_SolicitudRelevo_L8`, §6.3 |

### Matriz de permisos

| Operación | Coordinador | Supervisor | Supervisor L8 |
|---|---|---|---|
| Ver malla de cualquier línea | ✅ | ❌ | ❌ |
| Ver malla de su línea | ✅ | ✅ | n/a *(C7)* |
| Ver personal de otra línea | ✅ | ❌ | ❌ |
| **Ver restricciones médicas** | ✅ bajo demanda | ✅ solo su línea | ✅ solo su línea |
| Asignar en su línea | ✅ | ✅ | ✅ |
| Asignar en otra línea | ✅ | ❌ | ❌ |
| Aceptar/rechazar relevo | ✅ | ❌ | ✅ |
| Ver cola de relevos de la planta | ✅ | ❌ | ✅ proyección D1 |
| Despachar entre líneas | ✅ | ✅ desde la suya | ✅ |
| Confirmar recepción | ✅ | ✅ en la suya | ✅ |
| Registrar paro / cerrar lote | ✅ | ✅ en la suya | ❌ *(C7)* |
| Cerrar turno | ✅ forzado | ✅ el suyo | ✅ el suyo |
| Editar datos maestros | ✅ | ❌ | ❌ |
| Ejecutar excepción | ✅ con justificación | ❌ | ❌ |
| Extraer Operador B de otra línea | ✅ *(C15-N3)* | ❌ | ❌ |
| Cancelar tránsito | ✅ | ❌ | ❌ |
| Limpiar descartados | ✅ | ❌ | ✅ los suyos |

## 6.3 Aislamiento de datos `[SEGURIDAD DE DATOS]`

> §2.2: *"El aislamiento es total y deliberado. No es solo control de permisos: protege datos médicos y delimita de qué responde cada quien."*

**El aislamiento se implementa en tres capas, y ninguna sustituye a las otras:**

**Capa 1 — Vistas con proyección restringida.** El supervisor de L8 necesita ver puestos ajenos (§9.4 p2) pero no a las personas (D1). La vista expone exactamente los campos permitidos:

```sql
CREATE VIEW vw_SolicitudRelevo_L8 AS
SELECT
    sr.Id                AS solicitud_id,
    l.codigo             AS linea_codigo,
    p.codigo             AS puesto_codigo,
    p.tipo               AS puesto_tipo,
    sr.nivel,
    sr.exceso_relativo,
    sr.creada_en,
    p.perfil_preferente,
    (SELECT STRING_AGG(cf.nombre, ', ')
       FROM PuestoCapacidad pc
       JOIN CapacidadFisica cf ON cf.Id = pc.capacidad_id
      WHERE pc.puesto_id = p.Id)  AS capacidades_exigidas
FROM SolicitudRelevo sr
JOIN Puesto p ON p.Id = sr.puesto_id
JOIN Linea  l ON l.Id = p.linea_id
WHERE sr.resuelta_en IS NULL;
-- ⚠ NO expone personal_id, nombre, ficha ni restricciones médicas del ocupante.
```

**Capa 2 — Filtro obligatorio de alcance.** Toda consulta de un supervisor pasa por un filtro de línea aplicado en el repositorio, nunca en el controlador ni en el cliente.

**Capa 3 — Seguridad a nivel de fila (RLS) de SQL Server.** Red de seguridad ante un fallo de la capa 2:

```sql
CREATE FUNCTION dbo.fn_AlcanceLinea(@linea_id TINYINT)
RETURNS TABLE WITH SCHEMABINDING AS
RETURN SELECT 1 AS ok
       WHERE SESSION_CONTEXT(N'rol') = N'coordinador'
          OR CAST(SESSION_CONTEXT(N'linea_id') AS TINYINT) = @linea_id;

CREATE SECURITY POLICY PoliticaAlcanceLinea
    ADD FILTER PREDICATE dbo.fn_AlcanceLinea(linea_id) ON dbo.Puesto,
    ADD FILTER PREDICATE dbo.fn_AlcanceLinea(linea_id) ON dbo.JornadaLinea
WITH (STATE = ON);
```

> **Por qué tres capas y no una:** §2.2 califica el aislamiento de "total y deliberado" y lo vincula a la protección de datos médicos. Un solo `WHERE` olvidado en un refactor filtraría el padrón médico de otra línea. La RLS actúa aunque el código de aplicación falle: es defensa en profundidad, no redundancia.

## 6.4 Ciclo de tokens

```
Login (usuario + contraseña)
   → valida contra AD/Entra ID, o local
   → emite ACCESS TOKEN (JWT, 15 min) + REFRESH TOKEN (12 h, ligado al device_id)
   → claims: sub, rol, nombre  ── SIN linea_id (§2.3) ──

Cada petición
   → valida el access token
   → resuelve la línea EN VIVO desde Linea.supervisor_actual
   → fija SESSION_CONTEXT('rol') y ('linea_id') para la RLS

Expiración del access token
   → refresh silencioso si la sesión está activa

Inactividad prolongada
   → bloqueo de sesión → PIN de reentrada (D6)
   → 3 PIN fallidos → cierre de sesión y login completo

Cierre de turno / reasignación de línea
   → revoca la sesión, purga la caché local cifrada del dispositivo (D3)
```

---

# 7 · Reglas de integridad y transacciones

## 7.1 Dónde vive cada regla dura

| Regla | Dónde se sostiene |
|---|---|
| Una persona, una asignación activa *(§7.5)* | `UX_Asig_personal_activo` |
| Un puesto, una asignación activa | `UX_Asig_puesto_activo` |
| Tránsito inmune *(§6.1)* | `UX_Mov_transito` |
| Un puesto no se reserva dos veces *(B4)* | `UX_Mov_reserva` |
| Un supervisor, una línea *(§2.3)* | `UX_Linea_supervisor` |
| Un solo Bolsón *(§3.2)* | `UX_Linea_bolson` |
| Un lote abierto por línea *(C5)* | `UX_Lote_abierto` |
| Un paro abierto por línea *(§11.1)* | `UX_Paro_abierto` |
| Descripción de paro obligatoria *(§11.1)* | `CK_Paro_descripcion` |
| Justificación con contenido real *(A6)* | `CK_JE_texto` |
| Rechazo de recepción con motivo *(C10)* | `CK_Mov_rechazo` |
| **Restricción médica** *(§7.2)* | `sp_ValidarAsignacion` + `fn_TieneRestriccionBloqueante` |
| **Compatibilidad de categoría** *(§4.2)* | `sp_ValidarAsignacion` + `fn_CategoriaCompatible` |
| Aislamiento entre supervisores *(§2.2)* | Vistas + repositorio + RLS |

> **Las dos reglas médicas y de categoría no se pueden expresar como `CHECK`** porque dependen de varias tablas y de la fecha vigente. Por eso viven en un procedimiento almacenado **que es el único camino de escritura**: la aplicación no tiene permiso de `INSERT` directo sobre `Asignacion` (§7.3).

## 7.2 Validación central *(§7.1)*

```sql
CREATE OR ALTER FUNCTION dbo.fn_TieneRestriccionBloqueante
    (@personal_id INT, @puesto_id INT, @fecha DATE)
RETURNS BIT AS
BEGIN
    RETURN CASE WHEN EXISTS (
        SELECT 1
          FROM RestriccionMedica rm
          JOIN PuestoCapacidad  pc ON pc.capacidad_id = rm.capacidad_id
         WHERE rm.personal_id = @personal_id
           AND pc.puesto_id   = @puesto_id
           AND rm.fecha_inicio <= @fecha
           AND (rm.fecha_fin IS NULL OR rm.fecha_fin >= @fecha)   -- (C14)
    ) THEN 1 ELSE 0 END;
END;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ValidarAsignacion
    @personal_id INT, @puesto_id INT, @usuario_id INT,
    @permitir_ceder_perfil BIT = 0,
    @es_liderazgo_manual   BIT = 0,     -- (A7b)
    @codigo_rechazo VARCHAR(40) OUTPUT,
    @mensaje        NVARCHAR(400) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @hoy DATE = CAST(SYSUTCDATETIME() AS DATE);
    SET @codigo_rechazo = NULL;

    -- 1 · ¿El puesto sigue libre?
    IF EXISTS (SELECT 1 FROM Asignacion WHERE puesto_id = @puesto_id AND fin IS NULL)
    BEGIN SET @codigo_rechazo='PUESTO_OCUPADO'; RETURN; END

    -- 2 · ¿La persona sigue disponible?
    IF EXISTS (SELECT 1 FROM Personal WHERE Id=@personal_id
               AND situacion IN ('asignado','en_transito','retirado_temporal',
                                 'ausente_justificado'))
    BEGIN SET @codigo_rechazo='PERSONA_NO_DISPONIBLE'; RETURN; END

    -- 3 · ¿Categoría compatible? (§4.2) — salvo liderazgo manual (A7b)
    IF @es_liderazgo_manual = 0
       AND dbo.fn_CategoriaCompatible(@personal_id, @puesto_id) = 0
    BEGIN SET @codigo_rechazo='CATEGORIA_INCOMPATIBLE'; RETURN; END

    -- 4 · ¿Restricciones médicas?  [REGLA DURA] — no cede NUNCA (§7.2, B12)
    IF dbo.fn_TieneRestriccionBloqueante(@personal_id, @puesto_id, @hoy) = 1
    BEGIN SET @codigo_rechazo='RESTRICCION_MEDICA'; RETURN; END

    -- 5 · ¿Perfil preferente? [REGLA BLANDA] — única que cede (§7.3, §8.5)
    IF @permitir_ceder_perfil = 0
       AND dbo.fn_PerfilIncompatible(@personal_id, @puesto_id) = 1
    BEGIN SET @codigo_rechazo='PERFIL_PREFERENTE'; RETURN; END

    -- 6 · ¿No repitió la tarea en su jornada anterior? (§7.4, A4, B6)
    IF dbo.fn_ViolaNoRepeticion24h(@personal_id, @puesto_id) = 1
    BEGIN SET @codigo_rechazo='NO_REPETICION_24H'; RETURN; END

    -- 7 · ¿La ventana de arranque lo permite? (§8.4)
    IF dbo.fn_VentanaArranqueBloquea(@personal_id, @puesto_id) = 1
    BEGIN SET @codigo_rechazo='VENTANA_ARRANQUE'; RETURN; END
END;
```

> **El orden de los siete pasos es el del §7.1 y no se altera.** *"El primer rechazo detiene el proceso, para poder decir exactamente qué falló."* Reordenarlos por eficiencia produciría el mensaje equivocado.
>
> **`@permitir_ceder_perfil` solo afecta al paso 5.** No existe ningún parámetro capaz de saltar el paso 4. Es la traducción literal de *"las restricciones médicas no ceden en ningún nivel"* (§8.5, B12).
>
> **`@es_liderazgo_manual` solo afecta al paso 3**, y el servicio que lo activa exige `JustificacionExcepcion` (A7b).

## 7.3 Control de concurrencia *(§7.5, B1)*

```sql
CREATE OR ALTER PROCEDURE dbo.sp_AsignarPersona
    @personal_id INT, @puesto_id INT, @usuario_id INT,
    @jornada_linea_id INT, @origen VARCHAR(25),
    @idempotency_key UNIQUEIDENTIFIER,
    @ceder_perfil BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET TRANSACTION ISOLATION LEVEL READ COMMITTED;

    -- Idempotencia (§12.4: bloqueo contra doble toque)
    IF EXISTS (SELECT 1 FROM OperacionIdempotente WHERE clave = @idempotency_key)
    BEGIN
        SELECT resultado_json FROM OperacionIdempotente WHERE clave = @idempotency_key;
        RETURN;
    END

    BEGIN TRAN;
      -- Bloqueo determinista: SIEMPRE puesto antes que persona, para no
      -- generar interbloqueos entre dos supervisores simultáneos
      SELECT 1 FROM Puesto   WITH (UPDLOCK, HOLDLOCK) WHERE Id = @puesto_id;
      SELECT 1 FROM Personal WITH (UPDLOCK, HOLDLOCK) WHERE Id = @personal_id;

      DECLARE @cod VARCHAR(40), @msg NVARCHAR(400);
      EXEC dbo.sp_ValidarAsignacion @personal_id, @puesto_id, @usuario_id,
           @ceder_perfil, 0, @cod OUTPUT, @msg OUTPUT;

      IF @cod IS NOT NULL
      BEGIN ROLLBACK; THROW 51000, @msg, 1; END

      INSERT INTO Asignacion (jornada_linea_id, puesto_id, personal_id,
                              origen, asignado_por, cede_perfil)
      VALUES (@jornada_linea_id, @puesto_id, @personal_id,
              @origen, @usuario_id, @ceder_perfil);

      UPDATE Personal SET situacion = 'asignado' WHERE Id = @personal_id;

      EXEC dbo.sp_RegistrarAuditoria @usuario_id, 'ASIGNAR', 'Asignacion',
           SCOPE_IDENTITY(), @personal_id, 'OK';

      INSERT INTO OperacionIdempotente (clave, resultado_json)
      VALUES (@idempotency_key, '{"ok":true}');
    COMMIT;
END;
```

> **Cómo se materializa "gana uno"** *(B1)*: los dos `UPDLOCK, HOLDLOCK` serializan a los competidores; el índice único `UX_Asig_personal_activo` es la última red. **Gana la primera transacción que confirma en el servidor**, no el primer toque en pantalla — que es exactamente lo que decidimos, porque cualquier otro criterio haría perder al supervisor que tiene a la persona delante.
>
> **El orden de bloqueo es siempre puesto → persona**, invariable en todos los procedimientos. Un orden inconsistente entre dos procedimientos produce interbloqueos bajo carga de arranque, que es el momento de mayor concurrencia de la jornada.
>
> **`XACT_ABORT ON` + transacción única** implementa §7.5: *"Toda operación debe aplicarse completa o no aplicarse: no puede quedar el puesto ocupado y la persona libre, ni al revés."*

## 7.4 Catálogo de procedimientos almacenados

| Procedimiento | Qué garantiza | Ref. |
|---|---|---|
| `sp_ValidarAsignacion` | Las 7 reglas en orden, primer rechazo detiene | §7.1 |
| `sp_AsignarPersona` | Asignación atómica con idempotencia | §7.5, §12.4 |
| `sp_BarridoPuestosFijos` | Recorre por prioridad, **solo `tipo='fijo'`**, conserva titular | §8.3, C12 |
| `sp_SugerirPuesto` | Escalera de 4 niveles | §8.5 |
| `sp_DetectarFatiga` | Recalcula niveles con umbral **propio** de cada puesto | §9.1, A4 |
| `sp_ProponerRelevista` | Ranking B2, excluye descartados | B2, B10 |
| `sp_AceptarRelevo` | Tránsito + reserva atómicos | §9.4 p3 |
| `sp_ConfirmarRecepcion` | Llegada + asignación + sugerencia de destino del relevado | §9.4 p5-6 |
| `sp_SugerirDestinoRelevado` | Misma línea → proximidad → L8, con guarda de reserva | B4, A1 |
| `sp_ExtraccionInversa` | Orden derivado + piso de seguridad | §9.6, A5, B5 |
| `sp_CubrirVacanteCritica` | Escalera N1→N4 con guarda anti-dominó | C15 |
| `sp_RegistrarParo` | Libera rotativos, conserva fijos, genera tránsitos | §11.1 |
| `sp_CambiarSKU` | Recalcula puestos, cierra y abre lote | §11.2, C5 |
| `sp_CerrarLote` | Exige justificación sobre umbral, dispara recálculo | §11.3, C4 |
| `sp_CerrarTurno` | Verifica bloqueos, persiste última tarea, caduca descartes | C13, B6, B10 |
| `sp_CalcularEficiencia` | Fórmula del §11.4 **en el servidor** | §11.4, C4 |
| `sp_CaducarTransitos` | Marca demorados y alerta. **No mueve a nadie** | B11 |
| `sp_RegistrarAuditoria` | Traza obligatoria de toda operación | §12.7 |

## 7.5 Permisos de base de datos

```sql
-- La aplicación NO escribe directamente sobre las tablas críticas
DENY INSERT, UPDATE, DELETE ON Asignacion       TO rol_app;
DENY INSERT, UPDATE, DELETE ON Movimiento       TO rol_app;
DENY DELETE                  ON RestriccionMedica TO rol_app;   -- (C14)
DENY DELETE, UPDATE          ON Auditoria       TO rol_app;     -- (§12.7)
GRANT EXECUTE ON SCHEMA::dbo TO rol_app;
```

> **Esta es la garantía técnica de que "la decisión final nunca vive del lado del dispositivo"** (§7). Aunque alguien obtuviera credenciales de aplicación, no podría insertar una asignación saltándose `sp_ValidarAsignacion`: el único camino de escritura es el procedimiento que aplica las siete reglas.

---

# 8 · Auditoría y trazabilidad *(§12.7)*

```sql
CREATE TABLE Auditoria (
    Id             BIGINT IDENTITY PRIMARY KEY,
    usuario_id     INT NOT NULL REFERENCES Usuario(Id),
    rol            VARCHAR(15) NOT NULL,
    accion         VARCHAR(40) NOT NULL,
    entidad        VARCHAR(40) NOT NULL,
    entidad_id     BIGINT NULL,
    personal_id    INT NULL REFERENCES Personal(Id),     -- sobre quién
    linea_id       TINYINT NULL REFERENCES Linea(Id),
    resultado      VARCHAR(20) NOT NULL,                 -- OK | RECHAZO
    codigo_rechazo VARCHAR(40) NULL,
    datos_antes    NVARCHAR(MAX) NULL,
    datos_despues  NVARCHAR(MAX) NULL,
    justificacion_id BIGINT NULL REFERENCES JustificacionExcepcion(Id),
    device_id      NVARCHAR(120) NULL,
    ocurrido_en    DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_Aud_persona ON Auditoria(personal_id, ocurrido_en);
CREATE INDEX IX_Aud_linea   ON Auditoria(linea_id, ocurrido_en);
CREATE INDEX IX_Aud_rechazo ON Auditoria(codigo_rechazo, ocurrido_en)
    WHERE resultado = 'RECHAZO';
```

> §12.7: *"Toda operación que mueva a una persona debe quedar registrada: quién la hizo, cuándo, sobre quién y con qué resultado."*
>
> **Los rechazos también se auditan**, no solo los éxitos. `IX_Aud_rechazo` permite responder a la pregunta que hace auditable el cumplimiento ocupacional: *¿cuántas veces intentó alguien asignar a una persona a un puesto que su restricción médica prohibía?* Un sistema que solo registra lo que sí ocurrió no puede demostrar que impidió lo que no debía ocurrir.
>
> **Las horas de salida y llegada de cada movimiento** viven en `Movimiento` (§5.2), no aquí: son dato operativo con columna calculada e índice analítico propio, exactamente como §12.7 lo pide.

## 8.1 Retención *(D7)* 🟡

| Dato | Retención |
|---|---|
| Operativo: asignaciones, movimientos, paros, desperdicio, producción | **Indefinida** — materia prima del §12.7 |
| Auditoría | Indefinida |
| Restricciones médicas | Vigencia + periodo configurable (propuesta 5 años), después **anonimizado, no borrado** |

> ⚠ **`PENDIENTE-D7`.** Es propuesta técnica, no afirmación sobre el marco legal aplicable. Requiere validación del responsable legal o de salud ocupacional.

---

# 9 · Parámetros configurables *(§12.6)*

```sql
CREATE TABLE Parametro (
    clave          VARCHAR(60) NOT NULL PRIMARY KEY,
    valor          NVARCHAR(200) NOT NULL,
    tipo           VARCHAR(15) NOT NULL,
    descripcion    NVARCHAR(300) NOT NULL,
    modificado_por INT NULL REFERENCES Usuario(Id),
    modificado_en  DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME()
);
```

| Clave | Valor inicial | Origen |
|---|---|---|
| `ventana_arranque_min` | *a definir* | §8.4, §12.6 |
| `fatiga_sugerido_default_min` | *a definir* | §9.1, A4 |
| `fatiga_critico_default_min` | *a definir* | §9.1, A4 |
| `minimo_operarios_default` | *a definir* | §9.6, B5 |
| `factor_doble_turno` | **1.0** | B7 |
| `duracion_maxima_transito_min` | **15** *(provisional)* | B11 |
| `critico_sostenido_escalado_min` | *a definir* | B9 |
| `umbral_desperdicio_justificacion_pct` | *a definir* | §11.3 |
| `eficiencia_umbral_optimo_pct` | *a definir* | §11.4 |
| `eficiencia_umbral_aceptable_pct` | *a definir* | §11.4 |
| `antiguedad_maxima_datos_min` | **5** *(provisional)* | D4 |
| `inactividad_bloqueo_sesion_min` | *a definir* | D6 |
| `notificacion_acuse_timeout_min` | *a definir* | D5 |

> **Ninguno queda fijo en el código** *(§12.6)*. Los que aparecen como *a definir* se siembran vacíos deliberadamente: son datos del cliente y varios se calibran con operación real *(A4)*. Un valor por defecto inventado se convierte en dato de negocio sin que nadie lo haya decidido.
>
> Prioridad y proximidad de líneas **no** están aquí: tienen tablas propias porque son estructuras ordenadas, no valores sueltos (§2.2, §2.3).

---

# 10 · Notificaciones *(D5)*

```sql
CREATE TABLE Notificacion (
    Id           BIGINT IDENTITY PRIMARY KEY,
    usuario_id   INT NOT NULL REFERENCES Usuario(Id),
    tipo         VARCHAR(35) NOT NULL,
    criticidad   VARCHAR(10) NOT NULL DEFAULT 'normal',
    titulo       NVARCHAR(120) NOT NULL,
    cuerpo       NVARCHAR(300) NOT NULL,
    payload_json NVARCHAR(MAX) NULL,
    creada_en    DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    entregada_en DATETIME2(0) NULL,
    acusada_en   DATETIME2(0) NULL,
    escalada_en  DATETIME2(0) NULL,

    CONSTRAINT CK_Notif_criticidad CHECK (criticidad IN ('normal','critica'))
);

CREATE INDEX IX_Notif_sin_acuse ON Notificacion(criticidad, creada_en)
    WHERE acusada_en IS NULL;

-- Token de mensajería por dispositivo (D5)
CREATE TABLE DispositivoPush (
    Id            INT IDENTITY PRIMARY KEY,
    usuario_id    INT NOT NULL REFERENCES Usuario(Id),
    device_id     NVARCHAR(120) NOT NULL,
    push_token    NVARCHAR(400) NOT NULL,
    plataforma    VARCHAR(10) NOT NULL DEFAULT 'android',
    registrado_en DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    revocado_en   DATETIME2(0) NULL,

    CONSTRAINT UQ_DispositivoPush UNIQUE (device_id)
);

CREATE INDEX IX_Push_activo ON DispositivoPush(usuario_id)
    WHERE revocado_en IS NULL;
```

> **`push_token` identifica un teléfono, no a una persona de la plantilla.** Es el único dato que sale hacia el servicio de mensajería. El contenido de `titulo`, `cuerpo` y `payload_json` **nunca** viaja por ese canal: se descarga del servidor de planta por HTTPS después de que el ping despierte la app *(D5)*.
>
> El token se revoca al cerrar sesión, junto con la sesión y la purga de la caché local *(D3)*.

> **`entregada_en` / `acusada_en` / `escalada_en` son la capa 3 de D5.** El requisito del cliente es que las notificaciones lleguen aunque la app no esté abierta, y ninguna app Android puede garantizarlo al 100 %. Lo que sí se garantiza es que **nadie crea que se notificó cuando no se notificó**: una notificación crítica sin acuse escala al Coordinador y aparece en su panel como *"supervisor no localizable"*. Es el §1.3 aplicado a la infraestructura.

**Contenido restringido `[SEGURIDAD DE DATOS]`** *(D2)*: el aviso de fatiga a todos los supervisores lleva `"L4 · Puesto 3 — relevo sugerido · 62 min"` y **ninguna identidad de persona**, ni en `titulo`, ni en `cuerpo`, ni en `payload_json`.

---

# 10.1 · Versión de la aplicación *(F3)*

```sql
CREATE TABLE VersionApp (
    Id                INT IDENTITY PRIMARY KEY,
    version_nombre    VARCHAR(20) NOT NULL,      -- '1.4.2'
    version_codigo    INT NOT NULL UNIQUE,       -- entero incremental
    ruta_apk          NVARCHAR(300) NOT NULL,
    version_minima_api INT NOT NULL,             -- rompe compatibilidad por debajo
    notas             NVARCHAR(600) NULL,
    publicada_en      DATETIME2(0) NOT NULL DEFAULT SYSUTCDATETIME(),
    vigente           BIT NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX UX_VersionApp_vigente ON VersionApp(vigente) WHERE vigente = 1;
```

> **`version_minima_api` es lo que permite la convivencia de versiones** que exige el Anexo §3: *"distintas versiones de la app pueden convivir mientras el API mantenga compatibilidad"*. La app solo se bloquea si su código de versión queda por debajo de ese mínimo; en cualquier otro caso, **se ofrece la actualización pero no se impone**. Forzar a 11 dispositivos a actualizar a mitad de turno es exactamente lo que el anexo quería evitar.

---

# 11 · Estrategia de migración

## 11.1 Principios

1. **Migraciones versionadas, secuenciales e inmutables.** Una migración publicada nunca se edita; se corrige con otra nueva.
2. **Toda migración es reversible** o declara explícitamente por qué no lo es.
3. **Ninguna migración destruye datos en producción** sin una migración previa de respaldo.
4. **Compatibilidad hacia atrás obligatoria:** el anexo establece que *"distintas versiones de la app pueden convivir mientras el API mantenga compatibilidad"*. El esquema debe permitirlo.

## 11.2 Patrón de expansión y contracción

Todo cambio con riesgo se hace en tres despliegues separados:

```
DESPLIEGUE 1 — EXPANDIR
  · añadir la columna nueva como NULLABLE
  · el API escribe en ambas, lee de la vieja
  · sin cambio de comportamiento visible

DESPLIEGUE 2 — MIGRAR
  · rellenar la nueva por lotes (evita bloqueos largos)
  · el API lee de la nueva, sigue escribiendo en ambas
  · verificar consistencia

DESPLIEGUE 3 — CONTRAER
  · aplicar NOT NULL
  · dejar de escribir en la vieja
  · eliminar la vieja solo tras confirmar que ninguna app en uso la necesita
```

> **Por qué tres pasos y no uno:** hay más de 160 dispositivos y el anexo descarta explícitamente forzar que todos actualicen el mismo día. Un `ALTER TABLE` con `NOT NULL` en un solo despliegue rompe todas las apps que aún no actualizaron, en plena operación de turno.

## 11.3 Datos semilla

| Categoría | Contenido |
|---|---|
| **Estructural, inmutable** | 10 líneas, `es_bolson=1` en L8, `PrioridadLinea` base, `ProximidadLinea` completa **con la corrección A1** |
| **Catálogo, editable** | Capacidades físicas, categorías y causas de paro, motivos de excepción, motivos de rechazo |
| **Del cliente, vacío** | SKU, puestos, personal, turnos, usuarios |
| **Parámetros** | Los de §9, con los *a definir* deliberadamente vacíos |

## 11.4 Ventanas de despliegue

- Migraciones **solo entre turnos**, nunca con una jornada `arrancada`.
- Verificación previa obligatoria: cero movimientos `en_transito`, cero lotes abiertos.
- Respaldo completo antes de cada migración de contracción.
- Guion de reversión probado en preproducción antes de tocar producción.

---

# 12 · Trazabilidad

| Elemento | Origen |
|---|---|
| `Linea.es_bolson`, `UX_Linea_bolson` | §3.2, C7 |
| `Linea.minimo_operarios` nulable | §9.6, B5 |
| `UX_Linea_supervisor` | §2.3 |
| `PrioridadLinea` versionada | §3.3, §12.6, B8 |
| `ProximidadLinea` dirigida | §9.5, A1, A2, A3 |
| `TipoActividad.aplica_no_repeticion_24h` | §7.4, A4 |
| `Puesto.umbral_*` | §9.1, A4 |
| `Puesto.titular_id` doble semántica | §5.1, §8.5, C12 |
| `CapacidadFisica` + `PuestoCapacidad` | §7.2 |
| `Personal.perfil` nulable | §7.3 |
| `Personal.linea_habitual` | §8.2, C3 |
| `RestriccionMedica` con vigencia, sin borrado | §7.2, C14 |
| `UltimaTareaJornada` | §7.4, B6, C13 |
| `Lote`, `UX_Lote_abierto` | §11.3, C5 |
| `ProduccionAvance` | §11.4, C4 |
| `CK_Paro_descripcion` | §11.1 |
| `Asignacion.titular_original_id` | §8.3, C1 |
| `UX_Asig_*` | §7.5, B1 |
| `Movimiento.hora_salida/llegada/duracion_seg` | **§12.7** |
| `UX_Mov_transito` | §6.1 |
| `UX_Mov_reserva` | B4 |
| `CK_Mov_rechazo` | C10 |
| `RelevoDescartado.jornada_dia` | §9.4, B10 |
| `JustificacionExcepcion` | §2.1.9, A6 |
| Dos roles, L8 derivada | Parte II, C7 |
| Línea fuera del token | §2.3 |
| `vw_SolicitudRelevo_L8` | §2.2, §9.4, D1 |
| RLS por línea | §2.2 |
| `sp_ValidarAsignacion` orden de 7 | §7.1, B12 |
| `DENY` sobre tablas críticas | §7 (encabezado) |
| `Auditoria` con rechazos | §12.7 |
| `Parametro` | §12.6 |
| `Notificacion.acusada_en` | D5 |
| `DispositivoPush.push_token` | D5 |
| `VersionApp.version_minima_api` | Anexo §3, F3 |
| Expansión y contracción | Anexo §3 |
