# SmartAssign — Anexo de Arquitectura y Despliegue

**Nota técnica complementaria a la Especificación Funcional (v3.0).**
2026-08-08

> Este anexo existe aparte porque la especificación funcional declara explícitamente que no prescribe arquitectura ni tecnología. Aquí sí se prescribe, porque la empresa que aloja el proyecto ya opera **SQL Server** como estándar de base de datos en todos sus sistemas, y esa restricción de infraestructura tiene consecuencias directas sobre cómo debe construirse y desplegarse la app.

---

## 1. Plataforma

- **Cliente:** app nativa Android. Es la plataforma correcta dada la operación real descrita en la especificación (§12.3): guantes, una mano, de pie, escaneo de gafete, alcance del pulgar.
- **Base de datos:** SQL Server, por ser el estándar ya operado por la empresa (licencias, respaldos, personal de TI ya familiarizado).
- **Capa intermedia obligatoria:** un servidor de aplicación (API) entre el teléfono y SQL Server. **El teléfono nunca se conecta directamente a la base de datos.**

## 2. Por qué no una conexión directa Android → SQL Server

- **Autoridad central de validación (§7 de la especificación):** el documento exige que "la decisión final nunca puede quedar del lado del dispositivo". Eso solo se garantiza con lógica de negocio corriendo en un servidor, no repetida en cada teléfono.
- **Atomicidad y concurrencia (§7.5):** "toda operación debe aplicarse completa o no aplicarse" requiere transacciones controladas centralmente, no docenas de dispositivos escribiendo directo sobre la misma base.
- **Estado en tiempo real (§2.1):** ver las 10 líneas actualizándose en vivo requiere que un servidor empuje cambios a varios dispositivos a la vez; no es algo que resuelva una consulta SQL suelta desde cada teléfono.
- **Seguridad:** exponer el puerto de SQL Server directamente a decenas de dispositivos móviles en la red de planta es un riesgo considerable, y no existe una vía oficialmente soportada por Microsoft para esa conexión directa desde Android en producción.

## 3. Por qué esto facilita el despliegue

- Cada teléfono solo necesita un dato de configuración: **la URL del API.** Nada de credenciales de base de datos ni configuración de driver SQL en el dispositivo.
- Los parámetros configurables de la especificación (§12.6: jerarquía de prioridad, umbrales de fatiga, mínimos por línea, etc.) se ajustan **en el servidor**. No exige distribuir una nueva versión de la app cada vez que cambian.
- Los teléfonos solo necesitan salida HTTPS hacia un endpoint — no acceso al puerto de SQL Server dentro de la red de planta, que suele estar cerrado por buenas razones.
- Distintas versiones de la app pueden convivir mientras el API mantenga compatibilidad, sin forzar que los +160 dispositivos actualicen el mismo día.

## 4. Fuera de alcance de este anexo

Cómo llega el instalable (APK) a cada teléfono — vía MDM, Firebase App Distribution, o una pista interna de Google Play — es una decisión operativa independiente de lo anterior y no se cubre aquí.
