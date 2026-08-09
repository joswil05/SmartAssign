# SmartAssign — Estado de ejecución

**Se lee al empezar cada sesión. Se actualiza al terminar cada UT.**
Última actualización: 2026-08-09 · UTs completadas: **29 / 95**

## Decisiones de esta sesión que no estaban en los documentos

- **G1** cerrada: Operador A/B/C se simula tomando un subconjunto de OPERARIO (decisión del cliente); `OPERADOR DE EQUIPOS` → Operador A es **asunción propia**, revisar cuando lleguen datos reales de categorización. `OPERADOR DE CALDERAS`/`OPERARIO DE FILTROS Y TANQUERIA` → Operador A **confirmado por el cliente**.
- **G6** nueva y cerrada: `SexoPreferente` sucio en 9 filas de "Puestos Fijos" (H8) — `"Femenina"` era tipeo de `"Femenino"`, y 8 filas de la Línea 6 traían `PerfilRequerido` arrastrado por error a la columna equivocada. Reparado por patrón (confirmado por el cliente), no rechazado. Los 98 puestos reales ya importan.
- **G2** cerrada: datos médicos simulados por ahora (decisión del cliente).
- **G3** cerrada: las 10 líneas se mantienen en el modelo; L1/L2/L4/L6/L8 tienen datos reales hoy, el resto queda *inactiva* hasta que el Coordinador las planifique. `MAQUILA/PET/1605/1606` no son de las 10 líneas — quedan fuera de `linea_habitual` en el importador.
- **A14** nueva: `TiempoEnPuesto` del Excel puebla `umbral_sugerido` (renombrado `horas_en_puesto`); `umbral_critico` sigue nulable con default de planta, tal como A4 ya preveía — no hay un segundo dato real para él. Ver [00_DECISIONES.md §A14](00_DECISIONES.md).
- Skill externo `ui-ux-pro-max` vendorizado en `.claude/skills/ui-ux-pro-max-skill/` (no instalado como plugin — se invoca su script `search.py` directamente por ruta cuando llegue la etapa E6).
- **Backend apunta a .NET 10 / EF Core 10** (versión realmente instalada), no .NET 8 como decía el TRD original.
- Vulnerabilidad `NU1903` en `Microsoft.OpenApi` (dependencia transitiva del template `webapi` de .NET 10): actualizada a 2.3.0 (la más reciente disponible), sigue marcada por el feed de asesorías sin versión parcheada todavía. Rastreada en el CI como `continue-on-error`, no ignorada.

### Decisiones de ingeniería de la etapa E2 (delegadas, no de negocio — R2 no aplica)

- **AD/Entra ID no tiene adaptador todavía.** D6 dice "si la empresa lo tiene"; no hay entorno AD disponible en esta fase ni datos de conexión que nadie entregó. `IServicioAutenticacion` ya distingue `Usuario.OrigenIdentidad`; un usuario `"ad"` hoy rechaza el login con `ORIGEN_AD_NO_DISPONIBLE` en vez de fallar en silencio contra credenciales locales inexistentes. Queda listo para que un adaptador real lo implemente cuando el cliente provea el servicio.
- **Bloqueo automático por reintentos fallidos: mecanismo sin umbral.** El esquema (04 §6.1) ya trae `Usuario.IntentosFallidos`/`BloqueadoHasta`, y el login los respeta (si `BloqueadoHasta` está en el futuro, rechaza) — pero **nada fija ese campo automáticamente todavía**, porque ningún documento declara cuántos intentos ni cuántos minutos de bloqueo (no está ni en la tabla de `Parametro`, 04 §9). Inventar el número violaría R2. La tabla `Parametro` en sí **no tiene UT propia** en el plan — se creará cuando la primera regla que de verdad la necesite lo exija (candidata natural: E4 o E5).
- **El contador de PIN reutiliza `IntentosFallidos`.** El esquema declara una sola columna de reintentos por usuario, no una separada para login vs. PIN — reutilizarla es la lectura más fiel del dato declarado. Al tercer PIN fallido (04 §6.4, número explícito en el documento) se revoca la sesión activa del dispositivo y el contador se reinicia.
- **Login revoca otras sesiones activas del mismo `device_id`.** D6: "el teléfono se trata como compartido por línea". Es limpieza de concurrencia, no una regla de negocio nueva: al entrar un usuario en un teléfono, cierra cualquier sesión que hubiera quedado abierta de otro usuario en ese mismo aparato.
- **RLS (capa 3) solo cubre `Puesto` por ahora.** `JornadaLinea`, la otra tabla que 04 §6.3 menciona, no existe hasta E5 — una `SECURITY POLICY` no puede apuntar a una tabla inexistente. Su predicado se añade con `ALTER SECURITY POLICY` en la migración que cree esa tabla.
- **Primeros dos endpoints reales:** `GET /api/lineas` (coordinador) y `GET /api/lineas/{lineaId}` (coordinador o el supervisor dueño, vía `AlcanceLineaEndpointFilter`) — solo el resumen de la línea, no la malla de puestos (eso es E6). Existen para que la suite de aislamiento y PC-1 tengan algo real de la Api que golpear, y fijan el patrón (`RequireAuthorization()` + `AddEndpointFilter<AlcanceLineaEndpointFilter>()`) que todo endpoint de línea futuro debe repetir.
- **`JwtBearerOptions.MapInboundClaims = false`** — sin esto, el handler de validación remapea `"sub"` al URI largo heredado de WS-Federation y `FindFirst(ClaimsSmartAssign.UsuarioId)` deja de encontrar el claim. Gotcha conocido de ASP.NET Core, no una decisión de negocio.

> **Protocolo de sesión:**
> 1. Leer este fichero → identificar la siguiente UT sin marcar.
> 2. Leer **solo** lo que declara su columna `LEE`. Nunca la documentación entera.
> 3. Ejecutar. Si falta un dato de negocio → **detenerse y preguntar** *(regla R2)*.
> 4. Verificar en verde. Sin verde, la UT sigue abierta.
> 5. Marcar aquí + un commit citando el ID.

**Leyenda:** `[ ]` pendiente · `[~]` en curso · `[x]` verificada · `[!]` bloqueada

---

## E0 · Entorno *(3/3)* ✅

- [x] **E0.1** Fijar `JAVA_HOME` al JBR de Android Studio; comprobar Gradle y LocalDB
  - VERIFICADO: JDK 21 (JBR) responde · LocalDB `MSSQLLocalDB` responde con SQL Server 2025
- [x] **E0.2** Esqueleto de solución backend
  - VERIFICADO: `backend/SmartAssign.sln` con Domain/Application/Infrastructure/Api + 4 proyectos de prueba, referencias de Clean Architecture cableadas, `dotnet build` → 0 errores
- [x] **E0.3** Esqueleto Android + CI local
  - VERIFICADO: proyecto Compose (Kotlin, AGP 8.7.2, Compose BOM 2024.10.01, minSdk 26/targetSdk 36) · `gradlew assembleDebug` → BUILD SUCCESSFUL, `app-debug.apk` generado

## E1 · Cimientos y semilla estructural *(6/6)* ✅ → F0

- [x] **E1.1** Migración 001: líneas, puestos, catálogos base
  - VERIFICADO: migración `InicialEstructura` aplicada y revertida manualmente contra LocalDB, y de nuevo como prueba automatizada (`La_migracion_revierte_limpiamente`, `Reglas.SeguridadTests`)
- [x] **E1.2** Semilla estructural: 10 líneas, L8 Bolsón, prioridad base
  - VERIFICADO: 10 líneas, exactamente 1 con `es_bolson=1` (L8), 10 órdenes de prioridad vigentes distintos, L4 en orden 1 — por consulta directa y por prueba automatizada
- [x] **E1.3** Semilla `ProximidadLinea` — grafo dirigido, corrección A1
  - VERIFICADO: 81 filas (9 orígenes × 9 destinos; L8 sin filas como origen), sin repeticiones ni auto-referencias — prueba `Cada_una_de_las_9_lineas_origen_tiene_exactamente_9_destinos_sin_repetir`
- [x] **E1.4** Prueba dedicada de la fila de L10
  - VERIFICADO: `La_fila_de_L10_es_la_corregida_por_el_cliente` — `L9,L3,L6,L7,L4,L2,L1,L5,L8` exacto, más `La_proximidad_es_asimetrica_a_proposito_entre_L1_y_L5` (A3)
- [x] **E1.5** Semilla de capacidades físicas y catálogos de excepción/rechazo/paro
  - VERIFICADO: `CapacidadFisica` (6, placeholder — G2), `MotivoExcepcion` (7), `MotivoRechazoRecepcion` (4), `CategoriaParo`+`CausaParo` (4+5) sembrados vía `HasData`
- [x] **E1.6** Pipeline CI: build, test, arquitectura, secretos
  - VERIFICADO: `.github/workflows/backend-ci.yml` (build, arquitectura, unitarias, **reglas de seguridad bloqueante**, integración, auditoría de dependencias, TruffleHog) · corrido en local: **11/11 pruebas reales en verde**

**Nota de diseño de esta etapa:** los datos de semilla (`DatosEstructurales`, `DatosCatalogo`) se ubicaron en `SmartAssign.Domain/Semillas/`, no en `Infrastructure` — son datos de negocio (la tabla de proximidad corregida es una decisión de negocio, A1/A3), y el ORM solo los persiste vía `HasData`. Corrige un error de capa que cometí al primer intento y arreglé antes de compilar.

## E2 · Identidad y aislamiento *(6/6)* ✅ → F1

- [x] **E2.1** `Usuario`, `SesionDispositivo`, `DispositivoPush`
  - VERIFICADO: migración `IdentidadYAuditoria` aplicada y revertida (misma prueba automatizada de reversibilidad de E1, ahora cubre las 5 migraciones); FK real `Linea.SupervisorActualId → Usuario` añadida (antes columna simple)
- [x] **E2.2** JWT + refresh + PIN — **sin `linea_id` en el token**
  - VERIFICADO: `TokenClaimsTests` (4 pruebas) — `El_access_token_nunca_lleva_linea_id`, claims exactos `sub`/`rol`/`nombre`, expiración a 15 min, refresh opaco de un solo segmento; `CicloDeSesionTests` (6 pruebas) — refresh, PIN correcto, **3 PIN fallidos cierran la sesión (04 §6.4)**, PIN nunca abre sesión ajena, logout revoca refresh
- [x] **E2.3** Filtro de rol + filtro de alcance por línea
  - VERIFICADO: `AislamientoEntreSupervisoresTests` (6 pruebas, Api real vía `WebApplicationFactory`) — `Un_supervisor_de_L2_es_rechazado_en_el_endpoint_de_L4`, coordinador sin filtro, supervisor no puede listar todas, sin token → 401, supervisor sin línea asignada → 403
- [x] **E2.4** RLS en SQL Server como tercera capa
  - VERIFICADO: `AislamientoYAuditoriaTests` (3 pruebas de RLS) contra **conexión SQL cruda, sin ningún filtro de aplicación** — sin `SESSION_CONTEXT` bloquea todo (0 filas), coordinador ve todo, supervisor de L1 solo ve L1 aunque existan filas de L2. Solo cubre `Puesto`; `JornadaLinea` se añade en E5 (ver nota de ingeniería arriba)
- [x] **E2.5** `Auditoria` + `sp_RegistrarAuditoria`, **incluidos los rechazos**
  - VERIFICADO: `Un_login_exitoso_deja_una_fila_OK_en_Auditoria` y `Un_login_con_clave_equivocada_deja_una_fila_RECHAZO_en_Auditoria`, ambas vía el procedimiento almacenado real (Dapper), no un INSERT de EF
- [x] **E2.6** Suite de aislamiento completa
  - VERIFICADO: **32/32 pruebas reales en verde** en los 4 proyectos (Domain.UnitTests 4, Arquitectura.Tests 2, Api.IntegrationTests 6, Reglas.SeguridadTests 20) — incluida `Dos_supervisores_en_dos_telefonos_ninguno_ve_la_linea_del_otro`, la prueba automatizada exacta de PC-1

> **→ PC-1** · Validación humana: dos supervisores en dos teléfonos, ninguno ve la línea del otro. **Listo para validar** — requiere H2 (teléfonos físicos) y la app real (E6); por ahora demostrado por API + pruebas automatizadas.

## E3 · Personal y puestos *(6/6)* ✅ → F2

- [x] **E3.1** `Puesto` con umbrales propios nulables y `titular_id` de doble semántica
  - VERIFICADO: `CK_Puesto_umbrales` rechaza crítico=sugerido y crítico<sugerido (`PersonalYPuestosTests`), acepta crítico>sugerido y nulos. **Cambio de diseño respecto al 04 original:** el umbral crítico se guarda en horas (`umbral_critico_horas`), no minutos, para poder compararse contra `horas_en_puesto` sin conversión — ver nota de ingeniería abajo
- [x] **E3.2** `PuestoCapacidad` y `TipoActividad`
  - VERIFICADO: tabla `PuestoCapacidad` (FK a Puesto+CapacidadFisica) migrada y probada; la bandera `aplica_no_repeticion_24h` de la UT original **ya no existe** — A12 (cerrada antes de esta sesión) la sustituyó por `Puesto.horas_recuperacion` con dato real por puesto. Verificación real con las horas de Girar botellas(24)/Limpieza(48) queda para E3.6, cuando haya datos importados
- [x] **E3.3** `Personal` — `categoria` (mapeada de G1), `sexo` nulable, `linea_habitual`
  - VERIFICADO: `Personal_se_guarda_con_sexo_nulo_sin_error`. **Cambio de diseño respecto al 04 original:** el campo `perfil` (genérico) se sustituye por `sexo` — A13 (cerrada antes de esta sesión, no reflejada aún en 04) confirma que la regla blanda del §7.3 es la comparación de sexo contra `Puesto.sexo_preferente`. 04_ESQUEMA_BACKEND.md actualizado
- [x] **E3.4** `RestriccionMedica` con vigencia y sin borrado
  - VERIFICADO: `DENY_impide_borrar_una_restriccion_medica_con_la_cuenta_de_aplicacion` — impersonando un usuario real `rol_app` (`WITHOUT LOGIN`, creado en esta etapa) vía `EXECUTE AS`, con `GRANT SELECT/INSERT/UPDATE` explícito para aislar que lo único denegado es `DELETE`. Mensaje de SQL Server confirmado literalmente
- [x] **E3.5** **Semilla simulada adversaria** — los 16 escenarios
  - VERIFICADO: `SembradorAdversario` (Infrastructure) + `herramientas/ImportadorCli sembrar-adversaria` + `SemillaAdversariaTests` — 20 pruebas (16 escenarios, uno de ellos `[Theory]` por línea, más la prueba de separación de origen). Corrido de verdad contra `SmartAssignDev` tras el import real — ver el listado completo de los 16 en la nota de ingeniería abajo
- [x] **E3.6** Importador de datos reales con rechazo por lote
  - VERIFICADO: `ImportadorDatosRealesTests` (16 pruebas con libros sintéticos) + ejecución real contra `Base de Datos.xlsx` vía `herramientas/ImportadorCli importar`: **Personal 164/164 OK**, **Personal ausente 12/12 OK**, **Puestos Fijos 98/98 OK** tras la reparación de `SexoPreferente` confirmada por el cliente (00 §G6). El informe de rechazo original era real, no simulado — ver hallazgo e historial abajo

### Decisiones de ingeniería de la etapa E3 (delegadas, no de negocio — R2 no aplica)

- **`Puesto.PerfilRequerido` — columna nueva, no estaba en 04 original.** Al leer el Excel real (hoja "Puestos Fijos") para E3.1 apareció una columna real `PerfilRequerido` (Supervisor/Operador/Averiero/Estibador/Indistinto/Genérico/Operador de filtro) que 00 §A13 ya mencionaba pero que 04 nunca declaró como columna. Es la matriz de compatibilidad dura del §4.2, distinta de `categoria_titular`. Se añade la columna ahora (E3.1) porque el importador (E3.6) necesita dónde escribirla; el motor que la EVALÚA (`fn_CategoriaCompatible`) se construye en **E4**, no aquí — esta etapa solo declara el almacenamiento.
- **`Personal.Sexo` reemplaza `Personal.perfil`.** Mismo hallazgo: el Excel trae `Personal.Sexo` (MASCULINO/FEMENINO) siempre relleno, y A13 ya había cerrado que ese es el otro lado de la comparación con `Puesto.sexo_preferente`. La columna `Perfil` del Excel de Personal es en realidad la **categoría** (mapeada por G1), no una preferencia — nombre engañoso que se documenta explícitamente para que nadie más lo confunda.
- **`Puesto.UmbralCriticoHoras`, no `umbral_critico_min`.** El 04 original tenía los dos umbrales en minutos. A14 (cerrada antes de esta sesión) puebla el "sugerido" desde el dato real en HORAS (`horas_en_puesto`). Comparar horas contra minutos en la misma CHECK sin conversión habría sido un error silencioso; se define el crítico también en horas. Es una decisión técnica de continuidad de A14, no una cifra de negocio — sigue nulable, sin valor inventado.
- **Extensión de G1:** `OPERADOR DE CALDERAS` y `OPERARIO DE FILTROS Y TANQUERIA` (4 personas) no estaban en el resumen original de G1 — solo aparecen leyendo la hoja completa. Mapeadas a Operador A por la misma lógica que `OPERADOR DE EQUIPOS` (equipo específico, no tarea general); el cliente confirmó explícitamente esta parte (*"los de calderas y filtros cuéntalos como operadores"*, 2026-08-09) — queda cerrada para estas 4 personas. `OPERADOR DE EQUIPOS` sigue siendo inferencia propia sin confirmar. Ver `00_DECISIONES.md §G1` actualizado.
- **`rol_app` creado como `USER ... WITHOUT LOGIN`.** Necesario para que el DENY de RestriccionMedica (04 §7.5) sea probable de verdad — sin un principal separado de `dbo`, cualquier prueba de DENY sería un placebo, porque `db_owner` no queda sujeto a un DENY de esta forma. Qué login de servidor se mapea a este rol en cada entorno es una decisión de despliegue (F-block), no de esta migración.
- **El resto del DENY de 04 §7.5** (Asignacion, Movimiento, Auditoria) queda para **E4.7**, tal como decía el plan original — no se adelantó porque esas tablas todavía no tienen flujo de escritura real que proteger.
- **`00_DECISIONES.md §G5` (nueva, abierta, no bloqueante):** los 31 puestos fijos reales solo traen `PerfilRequerido` (Operador/Supervisor/Averiero/Genérico/Operador de filtro), que no mapea limpio a `categoria_titular` (`operador_a`/`operador_c`/`averiero`, 04 §2.6 original). Se relaja `CK_Puesto_categoria` — ya no exige el valor en fijos — en vez de inventar la categoría del titular. Bloquea eventualmente `sp_BarridoPuestosFijos` (E5.5), no la construcción de ahora.
- **Hallazgo real, no de diseño (resuelto el mismo día, ver G6):** al correr el importador contra el archivo real, **Puestos Fijos se rechazó — 9 de 98 filas tenían `SexoPreferente` contaminado con valores de `PerfilRequerido`** (`Femenina` ×1, `Supervisor` ×1, `Operador` ×4, `Averiero` ×1, `Estibador` ×2), tal como A13 ya advertía que podía pasar. El cliente confirmó que `"Femenina"` es tipeo de `"Femenino"`, y que el resto de la Línea 6 no tiene más error de captura del que ya se veía. El importador ahora **repara** esos valores conocidos por coincidencia de patrón (sin una sola excepción en las 91 filas limpias del archivo) en vez de rechazar la fila — `ImportadorDatosReales.ReparacionSexoPreferentePorArrastre`. Reimportado: **los 98 puestos fijos reales cargan limpio**, sin ninguna fila rechazada.
- **`Personal.OrigenDato` / `RestriccionMedica.OrigenDato` — columnas nuevas.** 07 §4.4 exige que "las filas simuladas lleven marca de origen" y que haya una prueba que falle si una llega a producción; no existía dónde guardar esa marca. Valores: `real` (default), `simulado` (persona/restricción inventada solo para probar el mecanismo), `simulado_categoria` (persona **real** del padrón cuya categoría se sobrescribió solo en esta semilla — la re-etiqueta de Operador B/C que pide G1).
- **`Base de Datos.xlsx` salió del control de versiones.** Traía PII real (cédula, INSS, fecha de nacimiento) y había quedado commiteado sin querer en una sesión anterior (`4b7b7e7`). Se agregó a `.gitignore` y se hizo `git rm --cached`; sigue en disco para que el importador lo siga usando. El archivo permanece en el historial de git de este repo local — si algún día se comparte o sube a un remoto, ese commit necesita limpiarse aparte (no basta con esto).
- **`herramientas/ImportadorCli`** — herramienta de línea de comandos nueva, con dos subcomandos (`importar`, `sembrar-adversaria`), nunca invocada desde la Api ni desde una migración. Fija el contexto de sesión como coordinador a mano (la Api real lo hace vía `ContextoSesionMiddleware`, etapa E2) porque estas herramientas no pasan por el pipeline HTTP.
- **Descubrimiento durante las pruebas: la RLS de `Puesto` (etapa E2) también protege las lecturas del propio importador.** Sin contexto de coordinador, el importador no vería sus propias filas al reimportar y produciría un `INSERT` duplicado en vez de una actualización. Se corrigió fijando el contexto antes de cualquier operación — tanto en la herramienta real como en las pruebas (`ComoCoordinadorAsync`).

**Los 16 escenarios de la semilla adversaria (07 §4.4), con su origen:**

| # | Escenario | Decisión |
|---|---|---|
| 1 | Restricción médica vigente que choca con el puesto | §7.2, C14 |
| 2 | Restricción caducada ayer → no bloquea | C14 |
| 3 | Restricción permanente (`fecha_fin` NULL) | C14 |
| 4 | Restricción que empieza mañana → no vigente todavía | C14 |
| 5 | Puesto con fatiga "sugerida" propia (`horas_en_puesto`) | A4, A14 |
| 6 | `umbral_critico_horas` válido, mayor que el sugerido | A4, A14 |
| 7 | "Girar botellas" con 24 h de recuperación | A12 |
| 8 | "Limpieza" con 48 h de recuperación | A12 |
| 9 | Sexo de la persona distinto al preferente del puesto (regla blanda) | A13, §7.3 |
| 10 | Sexo NULL → la regla no se evalúa | A13, §7.3 |
| 11 | Al menos un Operador B por línea activa (L1/L2/L4/L8) | G1 |
| 12 | Al menos un Operador C | G1 |
| 13 | L6 deliberadamente sin ningún Operador B → déficit | G1, prepara C15 |
| 14 | Titular ausente en L1 **con** Operador B disponible en su línea | C1 |
| 15 | Titular ausente en L6 **sin** Operador B en su línea | C1 |
| 16 | L2 exactamente en su piso mínimo; L4 una persona por encima | B5 |

## E4 · Motor de validación *(8/8)* ✅ → F3 · `[BLOQUEANTE]`

- [x] **E4.1** `fn_TieneRestriccionBloqueante` — solo vigentes
  - VERIFICADO: 5 pruebas — vigente bloquea, caducada no, permanente siempre, futura todavía no, sin restricción no bloquea
- [x] **E4.2** `fn_CategoriaCompatible` — matriz §4.2 completa
  - VERIFICADO: 21 casos (`[Theory]`) cubren las cuatro filas de la matriz casilla por casilla, incluidas todas las prohibidas, más el caso G5 (fijo sin `categoria_titular` → nadie compatible)
- [x] **E4.3** `fn_PerfilIncompatible` — regla blanda
  - VERIFICADO: puesto sin preferencia (NULL/Indistinto) no aplica · persona sin sexo registrado no aplica (nunca se infiere) · sexo distinto bloquea · sexo igual no
- [x] **E4.4** `fn_ViolaNoRepeticion24h` — generalizada (A12/B6, ver nota de ingeniería)
  - VERIFICADO: puesto sin horas de recuperación no tiene la regla · misma actividad dentro de la ventana bloquea · misma actividad ya fuera de la ventana (3 jornadas después) no bloquea · actividad distinta no bloquea
- [x] **E4.5** `fn_VentanaArranqueBloquea`
  - VERIFICADO: sin jornada abierta no bloquea · ventana abierta bloquea a quien no está físicamente en la línea · no bloquea a quien sí está · ventana ya cerrada no bloquea a nadie
- [x] **E4.6** `sp_ValidarAsignacion` — los 7 pasos en orden
  - VERIFICADO: 8 pruebas, una por paso — cada escenario hace fallar simultáneamente el paso bajo prueba y uno posterior, y se confirma que el código de rechazo es siempre el del paso más temprano. Confirmado explícitamente: ni `@permitir_ceder_perfil` ni `@es_liderazgo_manual` saltan el paso 4 (médica); `@permitir_ceder_perfil` es el único que cambia el resultado, y solo en el paso 5
- [x] **E4.7** `DENY` sobre tablas críticas
  - VERIFICADO: `INSERT` directo en `Asignacion` con `rol_app` falla con "INSERT permission was denied" (mismo patrón de impersonación `EXECUTE AS` que E3.4). `DELETE`/`UPDATE` en `Auditoria` denegados también. `Movimiento` queda pendiente — esa tabla no existe todavía (ver nota de ingeniería)
- [x] **E4.8** Suite de reglas de seguridad — médicas × 8 caminos
  - VERIFICADO: 8 pruebas — normal, cediendo perfil, liderazgo manual, la función de bajo nivel directamente, vigente hoy, permanente, caducada (control: no debe bloquear), e `INSERT` directo saltándose el procedimiento. Los 8 caminos están definidos y documentados en la nota de ingeniería de abajo — 05 §6.2 dice "médica × 8 caminos" sin enumerarlos

> **→ PC-2** · Validación humana: la restricción médica bloquea por los ocho caminos. **Lista para validar.**

### Decisiones de ingeniería de la etapa E4 (delegadas, no de negocio — R2 no aplica)

- **`fn_ViolaNoRepeticion24h` sintetiza A12 y B6, que nunca se habían puesto una junto a la otra.** A4/B6 (redactadas antes de tener datos reales) describían la regla como "solo Girar botellas, ventana = última jornada trabajada, no calendario". A12 (datos reales, etapa E3) la generalizó a un valor en HORAS propio de cada puesto (`horas_recuperacion`: 24 en Girar botellas, 48 en Limpieza) y jubiló la bandera booleana original. Ninguna decisión revisó la otra. La síntesis usada aquí es literal, no inventada: el ancla temporal es `UltimaTareaJornada.registrado_en` — que por diseño (C13) solo avanza al cerrar un turno realmente trabajado, nunca por el paso del calendario — y el umbral es `Puesto.horas_recuperacion`, el dato real. `docs/06_ROADMAP.md` (P3.4) todavía cita el lenguaje viejo de A4 ("solo la actividad marcada"); queda desactualizado por el mismo motivo que 04 §2.6/§3.1 lo estaban antes de la corrección de la etapa E3.
- **`TipoActividadId` solo se puebla para "Girar botellas".** Es el único agrupamiento que A14 confirma con dato real (las tres filas "Girar botellas 1/2/3" comparten `TiempoDeRecup=24`). Agrupar otros nombres de puesto por similitud de texto (p. ej. "Limpieza") inventaría una equivalencia que el dato no respalda — de hecho lo contradice: "Limpieza" tiene 5h de recuperación en L1 y 48h en L4/L6, tres valores distintos bajo el mismo nombre. El importador (E3.6) se extendió con este único caso; el resto de los 67 puestos rotativos reales queda con `tipo_actividad_id` NULL, lo que es correcto: sin agrupación confirmada, la regla de no repetición simplemente no aplica todavía a esos puestos.
- **`Parametro` se crea en esta etapa** (04 §9), exactamente como se anticipó en la nota de ingeniería de E2 ("candidata natural: E4 o E5"). `fn_VentanaArranqueBloquea` es la primera regla que de verdad necesita un parámetro configurable (`ventana_arranque_min`). Se sigue al pie de la letra `docs/06_ROADMAP.md` P5.1: *"NO inventes umbrales por defecto. Se leen de Parametro y pueden estar vacíos."* No se siembra ninguna fila para `ventana_arranque_min` — sigue "a definir" (04 §9) — y la función trata su ausencia como "la regla no aplica todavía", nunca como un valor por defecto inventado.
- **`JornadaLinea` y `Asignacion` se crean en esta etapa, incompletas a propósito.** `sp_ValidarAsignacion` (04 §7.2) necesita `Asignacion` para su paso 1, y `fn_VentanaArranqueBloquea` necesita saber cuándo cierra la ventana de una jornada — ninguna de las dos tablas existía todavía (el plan las agenda para E5.1/E5.2). `JornadaLinea` se crea aquí con solo las cuatro columnas que E4 necesita (`linea_id`, `arrancado_en`, `ventana_arranque_fin`, `cerrado_en`); el resto del diseño ya completo en 04 §4.1 (`turno_id`, `dia_operacion`, `sku_id`, `supervisor_id`, `estado`) se añade con `ALTER TABLE` cuando `Turno` se construya en E5.1 — mismo patrón ya usado con `Puesto` y `Personal` en E3. `Asignacion` sí se crea completa (04 §5.1 ya estaba totalmente especificado); lo que falta es `sp_AsignarPersona` (la escritura atómica con concurrencia, B1), que queda para cuando exista un endpoint real que la invoque.
- **`JustificacionExcepcion` se crea completa** (04 §5.4) porque `Asignacion.justificacion_id` la referencia por FK — aunque ningún flujo la escribe todavía (eso llega con las excepciones del Coordinador, A6, en etapas posteriores).
- **RLS extendida a `JornadaLinea` (04 §6.3).** La migración `RlsAlcanceLinea` de E2 ya había dejado escrito que el predicado de aislamiento sobre `JornadaLinea` se añadiría "en la migración que cree esa tabla" — es esta. `ALTER SECURITY POLICY ... ADD FILTER PREDICATE ... ON dbo.JornadaLinea`, mismo mecanismo que ya protege `Puesto` desde E2.
- **El DENY de `Movimiento` (04 §7.5) sigue pendiente.** La nota de la etapa E3 anticipaba que E4.7 cubriría "Asignacion, Movimiento, Auditoria" juntas; en la práctica `Movimiento` (Parte X, §5.2) todavía no existe como tabla — se crea en la etapa del motor de movimiento entre líneas (E8). El DENY se aplicará ahí, mismo criterio que ya se usó para no adelantar RLS sobre `JornadaLinea` en E2 cuando esa tabla tampoco existía.
- **Los 8 caminos de la suite médica (E4.8, PC-2) se definieron explícitamente, porque 05 §6.2 exige "médica × 8 caminos" sin enumerarlos.** Elegidos para cubrir literalmente cada cláusula de B12 ("ningún nivel, ningún motor, ningún rol, ninguna urgencia"): (1) camino normal, (2) cediendo perfil preferente, (3) con liderazgo manual (A7b), (4) la función de bajo nivel invocada directamente, (5) restricción vigente hoy, (6) restricción permanente, (7) restricción caducada — control negativo, para probar que la regla no se vuelve más estricta de lo que C14 pide — y (8) `INSERT` directo saltándose `sp_ValidarAsignacion` por completo (mismo mecanismo que E4.7).
- **`@mensaje` de `sp_ValidarAsignacion` es informativo, no la micro-copia literal de 02 §5.4.** El catálogo de 02 §5.4 interpola nombre, línea y minutos restantes — datos de presentación que le corresponde construir a la capa de aplicación cuando exista una UI real que los consuma (etapa E6), no a este procedimiento. Lo que E4.6 verifica y garantiza es `@codigo_rechazo`, que es lo que gobierna el orden y el comportamiento.

## E5 · Jornada, prioridad y barrido *(0/7)* → F4a

- [ ] **E5.1** `Turno` y día de operación · hora del servidor — LEE: `04 §4.1`, `00 §C6`
- [ ] **E5.2** `JornadaLinea` y `PrioridadLinea` versionada — LEE: `04 §2.2`, `00 §B8`
- [ ] **E5.3** `SKU`, `PuestoSKU`, cómputo de *fuera de operación* — LEE: `04 §2.5`, `§5.3`
- [ ] **E5.4** Planificación + rechazo si falta supervisor — LEE: `§8.1`, `02 §3.1`
- [ ] **E5.5** `sp_BarridoPuestosFijos` — por prioridad, **solo `tipo='fijo'`** — LEE: `§8.3`, `00 §C12`
- [ ] **E5.6** `titular_original_id`, vacante crítica, micro-copia §12.5 — LEE: `§8.3`, `§12.5`
- [ ] **E5.7** Ventana de arranque por jornada-línea — LEE: `§8.4`, `02 §4.1`

## E6 · App base y llenado *(0/8)* → F4b

- [ ] **E6.1** Sistema de diseño en Compose — LEE: `03 §2`, `00 §A11`
- [ ] **E6.2** Los cuatro estados de pantalla — LEE: `03 §3.11`, `§12.4`
- [ ] **E6.3** Login, PIN, alta de dispositivo por QR — LEE: `02 §1.0`, `02 §1.1`, `00 §F3`
- [ ] **E6.4** Malla de línea — LEE: `03 §3.1`, `03 §4.3`
- [ ] **E6.5** Escáner QR con ML Kit en dispositivo — LEE: `00 §E1`, `§12.1`
- [ ] **E6.6** Modal de confirmación de identidad — LEE: `03 §3.3`, `§12.2`
- [ ] **E6.7** `sp_SugerirPuesto` — escalera de 4 niveles — LEE: `§8.5`
- [ ] **E6.8** Concurrencia: bloqueo determinista + idempotencia — LEE: `04 §7.3`, `00 §B1`

> **→ PC-3** · **El punto de control más importante.** Un supervisor llena su línea desde un teléfono real.

## E7 · Fatiga *(0/4)* → F5

- [ ] **E7.1** Reloj desde `Asignacion.inicio` con umbral propio — LEE: `§9.1`, `00 §A4`
- [ ] **E7.2** Exceso relativo en % — LEE: `00 §A4`, `00 §B3`
- [ ] **E7.3** Tres niveles · fijos no acumulan · fatiga del puesto — LEE: `§9.1`, `§5.1`, `00 §A7`
- [ ] **E7.4** Factor de doble turno (default 1.0) + barra continua — LEE: `00 §B7`, `§11.5`

## E8 · Movimiento entre líneas *(0/6)* → F6

- [ ] **E8.1** Despacho con `hora_salida` — LEE: `Parte X`, `§12.7`, `04 §5.2`
- [ ] **E8.2** Tránsito inmune — LEE: `§6.1`, `04 §5.2`
- [ ] **E8.3** Recepción individual con `hora_llegada` — LEE: `00 §C8`, `03 §4.5`
- [ ] **E8.4** Rechazo con motivo obligatorio — LEE: `00 §C10`
- [ ] **E8.5** Reserva de puesto sin convergencia — LEE: `00 §B4`, `04 §5.2`
- [ ] **E8.6** `sp_CaducarTransitos` — alerta sin mover a nadie — LEE: `00 §B11`

## E9 · Motor de relevos *(0/9)* → F7

- [ ] **E9.1** `SolicitudRelevo` · el puesto no se libera al avisar — LEE: `§9.4 p1`, `04 §5.3`
- [ ] **E9.2** Orden de cola: crítico → exceso relativo → FIFO — LEE: `00 §B3`
- [ ] **E9.3** Aviso a todos los supervisores **sin identidad** — LEE: `00 §D2`, `§2.2`
- [ ] **E9.4** `vw_SolicitudRelevo_L8` — proyección mínima — LEE: `00 §D1`, `04 §6.3`
- [ ] **E9.5** `sp_ProponerRelevista` — ranking exacto — LEE: `00 §B2`
- [ ] **E9.6** Aceptar/rechazar + descartados con caducidad — LEE: `§9.4 p3`, `00 §B10`
- [ ] **E9.7** `sp_SugerirDestinoRelevado` — línea → proximidad → L8 — LEE: `00 §B4`, `00 §A1`, `00 §A9`
- [ ] **E9.8** Prueba del ejemplo normativo del §9.4 — LEE: `§9.4 ejemplo`, `00 §A8`
- [ ] **E9.9** Prueba de arquitectura: Relevos no referencia prioridad — LEE: `00 §A9`, `05 §4.1`

> **→ PC-4** · El ejemplo del §9.4 completo, con relevo en cadena.

## E10 · Extracción inversa y vacante crítica *(0/6)* → F8

- [ ] **E10.1** Orden derivado invirtiendo la prioridad — LEE: `00 §A5`, `§9.6`
- [ ] **E10.2** Solo con la L8 completamente vacía — LEE: `§9.6`
- [ ] **E10.3** Piso por línea · inmunidad — LEE: `00 §B5`
- [ ] **E10.4** Escalera C15 N1→N4 + guarda anti-dominó — LEE: `00 §C15`
- [ ] **E10.5** `JustificacionExcepcion` en toda excepción — LEE: `00 §A6`, `04 §5.4`
- [ ] **E10.6** Titular reincorporado + salida de retiro temporal — LEE: `00 §C1`, `00 §C2`

> ⚠ **Bloqueada hasta H7:** cerrar `A5b` (si L4 puede ser donante) y `A7-orig` (liderazgo y matriz de categoría).

## E11 · Contingencias y estadística *(0/8)* → F9

- [ ] **E11.1** Paro: dos niveles + descripción obligatoria — LEE: `§11.1`, `04 §4.3`
- [ ] **E11.2** Fijos ocupados · rotativos liberados con tránsito individual — LEE: `§11.1`, `00 §C8`
- [ ] **E11.3** Cronómetro persistente en todas las pantallas — LEE: `§11.1`, `03 §3.8`
- [ ] **E11.4** Relevista en tránsito hacia línea en paro — LEE: `00 §C9`
- [ ] **E11.5** `Lote` y cambio de SKU — LEE: `00 §C5`, `§11.2`
- [ ] **E11.6** Desperdicio + producción + justificación sobre umbral — LEE: `§11.3`, `00 §C4`
- [ ] **E11.7** Eficiencia calculada en el servidor — LEE: `§11.4`, `00 §C4`
- [ ] **E11.8** Todo registro empuja a los dos paneles — LEE: `00 §C4`

## E12 · Tiempo real y notificaciones *(0/6)* → F10

- [ ] **E12.1** Hub SignalR con grupos asignados por el servidor — LEE: `05 §2.4`, `§2.2`
- [ ] **E12.2** Catálogo de eventos — LEE: `05 §2.4`
- [ ] **E12.3** Bandeja de salida transaccional — LEE: `05 §4.1`
- [ ] **E12.4** FCM campana vacía + descarga del contenido real — LEE: `00 §D5`, `05 §2.5`
- [ ] **E12.5** Prueba que falla si la carga útil lleva negocio — LEE: `00 §D5`, `§12.1`
- [ ] **E12.6** Acuse, escalado y *"supervisor no localizable"* — LEE: `00 §D5`, `04 §10`

## E13 · Modo sin conexión *(0/5)* → F11

- [ ] **E13.1** Room + SQLCipher con clave en Keystore — LEE: `00 §D3`
- [ ] **E13.2** Caché acotada a su línea — LEE: `00 §D3`
- [ ] **E13.3** Bloqueo defensivo · **no se encola nada** — LEE: `§12.1`, `05 §4.3`
- [ ] **E13.4** Sello de frescura + degradación visual — LEE: `00 §D4`, `03 §3.7`
- [ ] **E13.5** Detección por latido — LEE: `05 §4.3`

> **→ PC-5** · Se corta la red y nada queda encolado.

## E14 · Cierre, histórico y endurecimiento *(0/7)* → F12

- [ ] **E14.1** Cierre de turno con lista exacta de bloqueos — LEE: `00 §C13`, `02 §4.10`
- [ ] **E14.2** `UltimaTareaJornada` + cierre forzado con justificación — LEE: `00 §B6`, `00 §A6`
- [ ] **E14.3** Histórico y auditoría consultable — LEE: `§2.1.11`, `§12.7`
- [ ] **E14.4** Rendimiento contra presupuestos — LEE: `05 §3.4`
- [ ] **E14.5** Accesibilidad: 48 dp, AAA, escala de grises — LEE: `03 §5`, `00 §A11`
- [ ] **E14.6** Distribución del APK + verificación de versión — LEE: `00 §F3`, `04 §10.1`
- [ ] **E14.7** Carga de datos reales + purga de lo simulado — LEE: `07 §4.3`

> **→ PC-6** · Piloto: un turno real completo, sin papel.

---

## Bloqueos activos

| ID | Qué falta | Bloquea |
|---|---|---|
| **H7** | Cerrar `A5b` y `A7-orig` | E10 |
| **H4** | Gafetes impresos con QR | Pruebas de campo desde E6 |
| **H6** | Vocabulario real de capacidades físicas | Producción, no construcción |
| **H2** | Teléfonos físicos | PC-1 en adelante |

> **H8 resuelto (2026-08-09, mismo día que se abrió):** el cliente confirmó que `"Femenina"` es error de tipeo de `"Femenino"`, y que el resto de la tabla (incluida la Línea 6) describe puestos y perfil técnico reales. Ver `00_DECISIONES.md §G6`. Los 98 puestos fijos reales ya importan sin errores.

## Registro de sesiones

| Fecha | UTs completadas | Notas |
|---|---|---|
| 2026-08-09 | E0 (3) + E1 (6) | Esqueletos backend/Android, migración base, semilla estructural con corrección A1 verificada |
| 2026-08-09 | E2 (6) | Identidad y aislamiento en tres capas (JWT sin `linea_id`, filtro de alcance, RLS). 32/32 pruebas en verde. Deuda documentada: bloqueo automático por reintentos sin umbral (falta `Parametro`, creada en E4), RLS de `JornadaLinea` diferida a cuando esa tabla exista (llegó en E4, no E5 como se estimaba aquí) |
| 2026-08-09 | E3.1–E3.4 (4) | `Personal`, extensión de `Puesto` (titular, umbrales en horas, `PerfilRequerido`), `PuestoCapacidad`, `RestriccionMedica` con `DENY DELETE` real vía `rol_app`. 38/38 pruebas en verde. Corrige 04 en dos puntos que A13/A14 ya habían cerrado pero el documento no reflejaba (`Personal.Sexo`, `Puesto.PerfilRequerido`). Extiende G1 con 4 personas no cubiertas por el resumen original. |
| 2026-08-09 | E3.5–E3.6 (2) | **E3 completa.** Importador real ejecutado contra `Base de Datos.xlsx`: Personal 164/164 y Ausencias 12/12 cargados; Puestos Fijos rechazado por 9 filas con dato contaminado (00 §A13, confirmado con datos reales, no solo predicho) — nuevo bloqueo H8, no crítico. Semilla adversaria de los 16 escenarios (07 §4.4) construida y corrida contra `SmartAssignDev`. Nueva decisión G5 (categoría de titular de puesto fijo no derivable). `Base de Datos.xlsx` sacado del control de versiones (traía PII real). 67/67 pruebas en verde en todo el backend |
| 2026-08-09 | H8 resuelto | Cliente confirmó `Femenina`=`Femenino` (tipeo) y que la Línea 6 no tiene más error de captura que el ya visto; calderas/filtros confirmados como Operador A. Nueva decisión G6: el importador repara `SexoPreferente` arrastrado por patrón en vez de rechazar. Reimportado: **98/98 Puestos Fijos reales cargan limpio**. 7 pruebas nuevas/ajustadas — 74/74 pruebas en verde en todo el backend |
| 2026-08-09 | E4 (8) | **Motor de validación completo — PC-2 lista para validar.** Las 5 funciones de §7.1 + `sp_ValidarAsignacion` con los 7 pasos exactos + DENY sobre `Asignacion`/`Auditoria` + suite médica de 8 caminos. Síntesis de ingeniería entre A12 y B6 para `fn_ViolaNoRepeticion24h` (ver nota abajo). Tablas nuevas: `Parametro`, `JustificacionExcepcion`, `JornadaLinea` (mínima), `Asignacion`, `UltimaTareaJornada` — todas ya especificadas en 04, traídas antes de su etapa porque E4 las necesita para compilar/probar. RLS extendida a `JornadaLinea` (04 §6.3, ya anticipada desde E2). 58 pruebas nuevas — 132/132 en verde en todo el backend |
