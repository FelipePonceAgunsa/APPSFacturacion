# Recrea la base local zarpe_facturacion aplicando la migración de EF Core (fuente del esquema desde la etapa 1)
# y comprueba que el esquema resultante es igual al de db/init (01-esquema.sql + 02-ajustes-etapa1.sql).
# Después corre las pruebas SQL y, al final, carga las semillas de db/semillas (datos de prueba inventados).
#
# BORRA los datos de la base local. Solo para desarrollo.
# Uso (desde db/, Windows PowerShell 5.1):  & .\recrear-con-migracion.ps1 *>&1
# Requiere: contenedor zarpe-postgres corriendo (docker compose up -d) y dotnet-ef instalado.

$ErrorActionPreference = 'Continue'   # los comandos nativos escriben avisos en stderr; los errores se revisan con $LASTEXITCODE

$contenedor = 'zarpe-postgres'
$base       = 'zarpe_facturacion'
$referencia = 'zarpe_referencia'
$usuario    = if ($env:ZARPE_DB_USER) { $env:ZARPE_DB_USER } else { 'postgres' }
$clave      = if ($env:ZARPE_DB_PASSWORD) { $env:ZARPE_DB_PASSWORD } else { 'postgres' }
$backend    = Join-Path $PSScriptRoot '..\Fuentes\AEXSA.ZARPE.BACKEND.NET'

function Fallar([string]$mensaje) {
    Write-Host "ERROR: $mensaje" -ForegroundColor Red
    exit 1
}

function Ejecutar-Sql([string]$db, [string]$sql) {
    docker exec -e PGOPTIONS=--client-min-messages=warning $contenedor psql -U $usuario -d $db -v ON_ERROR_STOP=1 -q -c $sql
    if ($LASTEXITCODE -ne 0) { Fallar "psql en ${db}: $sql" }
}

function Ejecutar-Archivo([string]$db, [string]$archivo) {
    $destino = '/tmp/' + [IO.Path]::GetFileName($archivo)
    docker cp $archivo "${contenedor}:$destino" | Out-Null
    if ($LASTEXITCODE -ne 0) { Fallar "docker cp $archivo" }
    $salida = docker exec -e PGOPTIONS=--client-min-messages=warning $contenedor psql -U $usuario -d $db -v ON_ERROR_STOP=1 -q -f $destino
    if ($LASTEXITCODE -ne 0) { $salida | Write-Host; Fallar "psql -f $archivo en $db" }
    return $salida
}

function Esquema([string]$db) {
    # Sin la tabla de historial de EF Core; sin comentarios, SET ni las marcas \restrict de pg_dump 16.10+.
    $dump = docker exec $contenedor pg_dump -U $usuario -s --no-owner --no-privileges '--exclude-table=\"__EFMigrationsHistory\"' $db
    if ($LASTEXITCODE -ne 0) { Fallar "pg_dump de $db" }
    return $dump | Where-Object {
        $_ -and $_.Trim() -ne '' -and -not $_.StartsWith('--') -and -not $_.StartsWith('SET ') -and
        -not $_.StartsWith('SELECT pg_catalog.set_config') -and -not $_.StartsWith('\restrict') -and -not $_.StartsWith('\unrestrict')
    }
}

docker exec $contenedor pg_isready -U $usuario -d postgres | Out-Null
if ($LASTEXITCODE -ne 0) { Fallar "El contenedor $contenedor no responde. Levantarlo con: docker compose up -d" }

Write-Host "1. Recreando la base $base (vacía)..."
Ejecutar-Sql 'postgres' "DROP DATABASE IF EXISTS $base WITH (FORCE)"
Ejecutar-Sql 'postgres' "CREATE DATABASE $base"

Write-Host "2. Aplicando la migración de EF Core..."
$conexion = "Host=localhost;Port=5433;Database=$base;Username=$usuario;Password=$clave"
Push-Location $backend
dotnet ef database update --project Satelite.Infraestructura --startup-project Satelite.Api --connection $conexion
$codigo = $LASTEXITCODE
Pop-Location
if ($codigo -ne 0) { Fallar 'dotnet ef database update' }

Write-Host "3. Creando la base de referencia $referencia con db/init (01 + 02)..."
Ejecutar-Sql 'postgres' "DROP DATABASE IF EXISTS $referencia WITH (FORCE)"
Ejecutar-Sql 'postgres' "CREATE DATABASE $referencia"
Ejecutar-Archivo $referencia (Join-Path $PSScriptRoot 'init\01-esquema.sql') | Out-Null
Ejecutar-Archivo $referencia (Join-Path $PSScriptRoot 'init\02-ajustes-etapa1.sql') | Out-Null

Write-Host "4. Comparando esquemas (pg_dump -s)..."
$deMigracion = @(Esquema $base)
$deReferencia = @(Esquema $referencia)
$diferencias = @(Compare-Object -ReferenceObject $deReferencia -DifferenceObject $deMigracion -SyncWindow ([Math]::Max($deReferencia.Count, $deMigracion.Count)))
if ($diferencias.Count -gt 0) {
    $diferencias | Format-Table -AutoSize | Out-String -Width 220 | Write-Host
    Ejecutar-Sql 'postgres' "DROP DATABASE IF EXISTS $referencia WITH (FORCE)"
    Fallar "$($diferencias.Count) diferencias de esquema (<= solo en db/init, => solo en la migración)"
}
Write-Host "   0 diferencias de esquema ($($deMigracion.Count) líneas comparadas)"

Write-Host "5. Pruebas SQL (db/pruebas/pruebas-etapa1.sql)..."
$salida = Ejecutar-Archivo $base (Join-Path $PSScriptRoot 'pruebas\pruebas-etapa1.sql')
$fallidas = $salida | Where-Object { $_ -match '\|\s*FALLA\s*\|' }
$fallidas | Write-Host
$totales = $salida | Where-Object { $_ -match '^\s*(\d+)\s*\|\s*(\d+)\s*$' } | Select-Object -Last 1
if (-not $totales -or $totales -notmatch '^\s*(\d+)\s*\|\s*(\d+)\s*$') { $salida | Write-Host; Fallar 'no se encontró el resumen de las pruebas' }
$ok = [int]$Matches[1]; $fallas = [int]$Matches[2]
Write-Host "   ok = $ok, fallas = $fallas"
if ($fallas -gt 0) { Fallar "$fallas pruebas SQL fallaron" }

# Las semillas van DESPUÉS de las pruebas: las pruebas insertan tipo_servicio 42 y tipo_inspeccion_mapa 'ADU-FIS'
# dentro de una transacción que se revierte, y chocarían con los mismos datos de prueba.
Write-Host "6. Semillas (db/semillas/*.sql)..."
$semillas = @(Get-ChildItem (Join-Path $PSScriptRoot 'semillas') -Filter '*.sql' -ErrorAction SilentlyContinue | Sort-Object Name)
foreach ($s in $semillas) {
    Ejecutar-Archivo $base $s.FullName | Out-Null
    Write-Host "   $($s.Name)"
}
if ($semillas.Count -eq 0) { Write-Host '   (no hay semillas)' }

Write-Host "7. Borrando la base de referencia..."
Ejecutar-Sql 'postgres' "DROP DATABASE IF EXISTS $referencia WITH (FORCE)"

Write-Host "Listo: $base recreada con la migración, esquema igual a db/init, ok = $ok, fallas = $fallas." -ForegroundColor Green
