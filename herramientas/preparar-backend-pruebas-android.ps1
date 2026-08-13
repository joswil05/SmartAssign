<#
.SYNOPSIS
    Deja lista la base descartable contra la que corren las pruebas de JVM
    de Android (`:app:testDebugUnitTest`), y opcionalmente levanta la Api.

.DESCRIPTION
    Las suites `SesionRepositorioIntegrationTest`, `MallaRepositorioIntegrationTest`
    y `PersonalRepositorioIntegrationTest` (18 pruebas) NO usan un servidor
    simulado: hablan por HTTP con la Api real de esta misma rama, contra la
    base `SmartAssignAndroidJvmTest2` en el puerto 5081.

    Ese fixture no sobrevive entre sesiones (ni el proceso `dotnet run`, ni
    los usuarios, ni el puesto/persona de prueba), y hasta ahora había que
    recrearlo a mano cada vez — docs/PROGRESO.md lo tenía anotado como
    candidato a automatizar desde E6.5. Esto es esa automatización.

    Es idempotente: se puede volver a correr sin romper nada. Las
    migraciones se reaplican solo si faltan, y `crear-usuario-prueba` ya
    reasigna correctamente la línea de un usuario que exista (corregido en
    E6.4).

    SOLO dev/test — la cadena de conexión es LocalDB y `ImportadorCli`
    rechaza cualquier cadena que contenga "Prod" (00 §G2).

.PARAMETER Levantar
    Además de sembrar, arranca `dotnet run` en el puerto 5081 y espera a
    que la Api responda antes de devolver el control.

.EXAMPLE
    ./herramientas/preparar-backend-pruebas-android.ps1
    ./herramientas/preparar-backend-pruebas-android.ps1 -Levantar
#>
param(
    [switch]$Levantar
)

# A propósito NO 'Stop': en Windows PowerShell 5.1, cualquier línea que un
# ejecutable nativo escriba en stderr se convierte en un ErrorRecord y
# aborta el script — y `dotnet` escribe ahí el warning NU1903 de
# Microsoft.OpenApi en cada invocación, aunque termine con éxito. El
# control de errores real de este script es la comprobación explícita de
# $LASTEXITCODE tras cada llamada nativa, que es la señal fiable.
$ErrorActionPreference = 'Continue'

$raiz = Split-Path -Parent $PSScriptRoot
$backend = Join-Path $raiz 'backend'
$conexion = 'Server=(localdb)\MSSQLLocalDB;Database=SmartAssignAndroidJvmTest2;Trusted_Connection=True;TrustServerCertificate=True;'
$puerto = 5081

Write-Host '== 1/4 · Migraciones al día ==' -ForegroundColor Cyan
dotnet ef database update `
    --project (Join-Path $backend 'SmartAssign.Infrastructure') `
    --startup-project (Join-Path $backend 'SmartAssign.Api') `
    --connection $conexion
if ($LASTEXITCODE -ne 0) { throw 'Falló la aplicación de migraciones.' }

Write-Host '== 2/4 · Usuarios de prueba (contraseñas reales, no simuladas) ==' -ForegroundColor Cyan
# Los 6 que esperan las suites. La línea 9 es la que MallaRepositorioIntegrationTest
# consulta como "su línea"; L1 es la del supervisor que debe recibir SinAlcance.
$usuarios = @(
    @{ user = 'coord_android';        pass = 'Clave#Coord123';    rol = 'coordinador'; linea = $null },
    @{ user = 'sup_l4_android';       pass = 'Clave#SupL4123';    rol = 'supervisor';  linea = 4    },
    @{ user = 'sup_l8_android';       pass = 'Clave#SupL8123';    rol = 'supervisor';  linea = 8    },
    @{ user = 'sup_sinlinea_android'; pass = 'Clave#SinLinea123'; rol = 'supervisor';  linea = $null },
    @{ user = 'sup_l4_malla';         pass = 'Clave#Malla123';    rol = 'supervisor';  linea = 9    },
    @{ user = 'sup_l1_malla';         pass = 'Clave#Malla456';    rol = 'supervisor';  linea = 1    }
)

$cli = Join-Path $backend 'herramientas/ImportadorCli'

# Se compila UNA vez y se invoca el binario en cada iteración. Seis
# `dotnet run` seguidos sobre el mismo proyecto fallan de forma
# intermitente en Windows: el proceso anterior todavía tiene tomado
# ImportadorCli.exe cuando el siguiente intenta reescribirlo al construir.
Write-Host '   (compilando ImportadorCli una sola vez)'
dotnet build $cli -c Debug --nologo -v quiet | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de ImportadorCli.' }
$cliDll = Join-Path $cli 'bin/Debug/net10.0/ImportadorCli.dll'
if (-not (Test-Path $cliDll)) { throw "No se encontró el binario compilado: $cliDll" }

foreach ($u in $usuarios) {
    Write-Host "   · $($u.user)"
    if ($null -eq $u.linea) {
        dotnet $cliDll crear-usuario-prueba $u.user $u.pass $u.rol $conexion | Out-Null
    } else {
        dotnet $cliDll crear-usuario-prueba $u.user $u.pass $u.rol $u.linea $conexion | Out-Null
    }
    if ($LASTEXITCODE -ne 0) { throw "Falló crear-usuario-prueba para $($u.user)." }
}

Write-Host '== 3/4 · Puesto y persona de prueba ==' -ForegroundColor Cyan
# Puesto tiene RLS (04 §6.3): sin SESSION_CONTEXT de coordinador, ni el
# propio script vería las filas que acaba de escribir. Mismo motivo por el
# que ImportadorCli fija el contexto (hallazgo de E3.6).
$siembra = @'
EXEC sys.sp_set_session_context @key = N'rol', @value = N'coordinador';

-- L4-JVM01: fijo en la línea 9, SIN jornada abierta, para que su situación
-- real sea 'fuera_de_operacion' (lo que la prueba afirma). No se siembra
-- ninguna JornadaLinea a propósito.
IF NOT EXISTS (SELECT 1 FROM Puesto WHERE codigo = 'L4-JVM01')
    INSERT INTO Puesto (linea_id, codigo, nombre_puesto, tipo)
    VALUES (9, 'L4-JVM01', 'Puesto de prueba JVM', 'fijo');

-- F-JVM01 + su restricción médica vigente: PersonalRepositorioIntegrationTest
-- comprueba que §12.2 devuelve nombre, categoría y restricciones explícitas.
-- `origen_dato = 'simulado'` es obligatorio (07 §4.4): la prueba que impide
-- que una fila inventada llegue a producción se apoya en esa marca.
IF NOT EXISTS (SELECT 1 FROM Personal WHERE ficha = 'F-JVM01')
    INSERT INTO Personal (ficha, nombre_completo, categoria, situacion, origen_dato)
    VALUES ('F-JVM01', N'María López Hernández', 'operario', 'fuera_de_turno', 'simulado');

-- La restricción que la app muestra es CapacidadFisica.nombre (el endpoint
-- proyecta r.Capacidad.Nombre) — no un texto libre en RestriccionMedica.
-- Esta capacidad NO viene en la semilla base de 6 (E1.5): es propia del
-- fixture, con su código propio para no chocar con las reales.
IF NOT EXISTS (SELECT 1 FROM CapacidadFisica WHERE codigo = 'CAP-JVM01')
    INSERT INTO CapacidadFisica (codigo, nombre)
    VALUES ('CAP-JVM01', N'No levantar carga superior a 10 kg');

DECLARE @personal_id INT = (SELECT Id FROM Personal WHERE ficha = 'F-JVM01');
DECLARE @capacidad_id INT = (SELECT Id FROM CapacidadFisica WHERE codigo = 'CAP-JVM01');
DECLARE @usuario_id INT = (SELECT TOP 1 Id FROM Usuario WHERE username = 'coord_android');

-- fecha_fin NULL = permanente (escenario 3 de la semilla adversaria, C14):
-- vigente siempre, así la prueba no caduca con el paso del tiempo.
IF NOT EXISTS (SELECT 1 FROM RestriccionMedica WHERE personal_id = @personal_id AND capacidad_id = @capacidad_id)
    INSERT INTO RestriccionMedica (personal_id, capacidad_id, fecha_inicio, fecha_fin, fuente, fecha_dictamen, registrado_por, origen_dato)
    VALUES (@personal_id, @capacidad_id, '2020-01-01', NULL, N'Enfermería', '2020-01-01', @usuario_id, 'simulado');
'@

$sqlTemp = Join-Path ([System.IO.Path]::GetTempPath()) 'smartassign-siembra-android.sql'
Set-Content -Path $sqlTemp -Value $siembra -Encoding utf8
# -f 65001: sin esto sqlcmd lee el fichero como ANSI y "María López
# Hernández" llega mutilado a la base — la prueba compara el nombre
# carácter por carácter, así que el fallo sería real, no cosmético.
sqlcmd -S '(localdb)\MSSQLLocalDB' -d 'SmartAssignAndroidJvmTest2' -f 65001 -i $sqlTemp
if ($LASTEXITCODE -ne 0) { throw 'Falló la siembra de puesto/persona.' }
Remove-Item $sqlTemp -Force

if (-not $Levantar) {
    Write-Host ''
    Write-Host '== 4/4 · Listo (sin levantar la Api) ==' -ForegroundColor Green
    Write-Host "Para correr las pruebas, en OTRA terminal:" -ForegroundColor Yellow
    Write-Host "  cd backend/SmartAssign.Api" -ForegroundColor Yellow
    Write-Host "  `$env:ConnectionStrings__SmartAssignDb='$conexion'" -ForegroundColor Yellow
    Write-Host "  dotnet run --urls http://localhost:$puerto" -ForegroundColor Yellow
    exit 0
}

Write-Host "== 4/4 · Levantando la Api en el puerto $puerto ==" -ForegroundColor Cyan
$env:ConnectionStrings__SmartAssignDb = $conexion
$api = Start-Process -PassThru -NoNewWindow -FilePath 'dotnet' `
    -ArgumentList 'run', '--project', (Join-Path $backend 'SmartAssign.Api'), '--urls', "http://localhost:$puerto"

# Espera activa a que responda de verdad — no un sleep a ciegas.
$listo = $false
foreach ($i in 1..60) {
    Start-Sleep -Seconds 1
    try {
        Invoke-WebRequest -Uri "http://localhost:$puerto/api/servidor/info" -TimeoutSec 2 -UseBasicParsing | Out-Null
        $listo = $true
        break
    } catch { }
}

if (-not $listo) {
    $api | Stop-Process -Force -ErrorAction SilentlyContinue
    throw "La Api no respondió en el puerto $puerto tras 60 s."
}

Write-Host ''
Write-Host "Api lista en http://localhost:$puerto (PID $($api.Id))." -ForegroundColor Green
Write-Host 'Ya se pueden correr las pruebas de JVM de Android:' -ForegroundColor Green
Write-Host '  cd android; ./gradlew :app:testDebugUnitTest' -ForegroundColor Yellow
Write-Host "Para detenerla:  Stop-Process -Id $($api.Id)" -ForegroundColor Yellow
