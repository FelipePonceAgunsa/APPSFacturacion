# Genera db/init/01-esquema.sql a partir de los bloques ```sql de docs/arquitectura/modelo-datos.md.
# El modelo de datos es la fuente: si cambia, vuelve a correr este script y recrea el contenedor
# (docker compose down -v; docker compose up -d).
$ErrorActionPreference = 'Stop'
$raiz   = Split-Path -Parent $PSScriptRoot
$modelo = Join-Path $raiz 'docs\arquitectura\modelo-datos.md'
$salida = Join-Path $PSScriptRoot 'init\01-esquema.sql'

$lineas = Get-Content -LiteralPath $modelo -Encoding UTF8
$version = ($lineas | Where-Object { $_ -match '^Versi.n:' } | Select-Object -First 1)

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('-- GENERADO desde docs/arquitectura/modelo-datos.md con db/generar-esquema.ps1. No editar a mano.')
[void]$sb.AppendLine("-- Modelo: $version")
[void]$sb.AppendLine('')
[void]$sb.AppendLine('CREATE EXTENSION IF NOT EXISTS pgcrypto;  -- gen_random_uuid() (seccion 5 del modelo)')
[void]$sb.AppendLine('')

$dentro = $false
$seccion = ''
foreach ($l in $lineas) {
    if (-not $dentro -and $l -match '^###\s+(.*)$') { $seccion = $Matches[1] }
    if (-not $dentro -and $l -match '^```sql\s*$') {
        $dentro = $true
        [void]$sb.AppendLine("-- ===== $seccion")
        continue
    }
    if ($dentro -and $l -match '^```\s*$') { $dentro = $false; [void]$sb.AppendLine(''); continue }
    if ($dentro) { [void]$sb.AppendLine($l) }
}

New-Item -ItemType Directory -Force -Path (Split-Path $salida) | Out-Null
[IO.File]::WriteAllText($salida, $sb.ToString(), [Text.UTF8Encoding]::new($false))
"Esquema generado: $salida"
