# Prueba de punta a punta de la etapa 1 contra la API local en modo abierto (rol Administrador).
# Datos inventados de datos-prueba/. Uso, con la base recreada (db/recrear-con-migracion.ps1) y la API corriendo:
#   & .\datos-prueba\e2e-etapa1.ps1 *>&1
#   & .\datos-prueba\e2e-etapa1.ps1 -Api http://localhost:5228 *>&1
# Se detiene en el primer paso que falla e indica cuál (código de salida 1).
# Pasos 1 a 4: catálogo y convenios (bloques 2 y 3). Los bloques 4 y 5 agregan sus pasos al final.
param(
    [string]$Api = 'http://localhost:5228',
    # Fecha con la que se busca el convenio vigente (la del evento de AEXSA de ejemplo).
    [string]$FechaVigencia = '2026-10-26'
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$utf8 = New-Object System.Text.UTF8Encoding($false)
$script:pasoActual = ''

function Fallar([string]$detalle) {
    Write-Host "FALLA en el paso '$script:pasoActual': $detalle" -ForegroundColor Red
    exit 1
}

function Paso([string]$nombre, [scriptblock]$cuerpo) {
    $script:pasoActual = $nombre
    Write-Host "== $nombre"
    try {
        & $cuerpo
    }
    catch {
        $respuesta = $_.ErrorDetails.Message
        Fallar "$($_.Exception.Message) $respuesta"
    }
    Write-Host "   OK" -ForegroundColor Green
}

# JSON en UTF-8 (Invoke-RestMethod de PowerShell 5.1 no lo codifica solo).
function Llamar([string]$metodo, [string]$ruta, $cuerpo = $null) {
    $parametros = @{ Method = $metodo; Uri = "$Api$ruta"; ContentType = 'application/json; charset=utf-8' }
    if ($null -ne $cuerpo) {
        $json = if ($cuerpo -is [string]) { $cuerpo } else { $cuerpo | ConvertTo-Json -Depth 10 }
        $parametros.Body = $utf8.GetBytes($json)
    }
    Invoke-RestMethod @parametros
}

# multipart/form-data con curl.exe (Invoke-RestMethod -Form no existe en PowerShell 5.1).
function SubirMultipart([string]$ruta, [string[]]$partes) {
    $salida = [System.IO.Path]::GetTempFileName()
    $argumentos = @('-s', '-o', $salida, '-w', '%{http_code}')
    foreach ($p in $partes) { $argumentos += @('-F', $p) }
    $codigo = & curl.exe @argumentos "$Api$ruta"
    $cuerpo = [System.IO.File]::ReadAllText($salida, $utf8)
    Remove-Item $salida
    if ($codigo -ne '201') { throw "HTTP $codigo en POST ${ruta}: $cuerpo" }
    $cuerpo | ConvertFrom-Json
}

Paso '0. La API responde en modo abierto como Administrador' {
    $yo = Llamar GET '/api/Session/Me'
    if ($yo.roles -notcontains 'Administrador') { throw "roles: $($yo.roles -join ', ')" }
}

Paso '1. Importar y confirmar el catálogo SAP de ejemplo (COMPLETO, A1 a A4)' {
    $c = Join-Path $raiz 'catalogo'
    $r = SubirMultipart '/api/catalogo/importaciones' @(
        'tipo=COMPLETO',
        "articulos=@$c\A1_articulos_20261101.csv",
        "listasPrecios=@$c\A2_listas_precios_20261101.csv",
        "kits=@$c\A3_kits_20261101.csv",
        "tipoCambio=@$c\A4_tipo_cambio_20261101.csv")
    if ($r.estado -ne 'PENDIENTE_CONFIRMACION') { throw "estado $($r.estado): $($r.errores | ConvertTo-Json -Compress)" }
    $resumen = Llamar POST "/api/catalogo/importaciones/$($r.importacionId)/confirmar"
    Write-Host "   catálogo versión $($resumen.version): $($resumen.articulos) artículos, $($resumen.listasPrecios) listas, $($resumen.kits) kits, TC al $($resumen.tipoCambioAl)"
    if ($resumen.articulos -lt 11) { throw "se esperaban al menos 11 artículos" }

    $rechazo = SubirMultipart '/api/catalogo/importaciones' @('tipo=LISTAS_PRECIOS', "archivo=@$c\error_A2_articulo_inexistente.csv")
    if ($rechazo.estado -ne 'RECHAZADA' -or -not ($rechazo.errores | Where-Object codigo -eq 'ARTICULO_NO_EXISTE')) {
        throw "el archivo con un artículo inexistente debía quedar RECHAZADA con ARTICULO_NO_EXISTE"
    }
}

Paso '2. Vincular los servicios 42 a 52 con su artículo SAP' {
    $vinculos = [ordered]@{
        42 = 'SRV-REC-001'; 43 = 'SRV-DES-001'; 44 = 'SRV-MOV-001'; 45 = 'SRV-VIS-001'; 46 = 'SRV-SEC-001'; 47 = 'SRV-REF-001'
        48 = 'SRV-ALM-001'; 49 = 'SRV-SAG-001'; 50 = 'SRV-USO-001'; 51 = 'SRV-DOC-001'; 52 = 'SRV-MAN-001'
    }
    foreach ($par in $vinculos.GetEnumerator()) {
        $id = $par.Key
        $v = Llamar PUT "/api/catalogo/vinculos/$id" @{ articuloSap = $par.Value }
        if ($v.estado -ne 'CONFIRMADO') { throw "servicio ${id}: estado $($v.estado)" }
    }
}

$script:convenios = @{}
Paso '3. Crear y confirmar los convenios de ejemplo, y verlos guardados como VIGENTE' {
    foreach ($archivo in Get-ChildItem (Join-Path $raiz 'convenios') -Filter 'convenio-*.json' | Sort-Object Name) {
        $cuerpo = [System.IO.File]::ReadAllText($archivo.FullName, $utf8)
        $borrador = Llamar POST '/api/convenios' $cuerpo
        if ($borrador.estado -ne 'BORRADOR') { throw "$($archivo.Name): nació $($borrador.estado)" }
        $null = Llamar POST "/api/convenios/$($borrador.convenioId)/versiones/$($borrador.versionId)/confirmar"
        $detalle = Llamar GET "/api/convenios/$($borrador.convenioId)"
        $guardada = $detalle.versiones | Where-Object versionId -eq $borrador.versionId
        if ($guardada.estado -ne 'VIGENTE') { throw "$($archivo.Name): quedó $($guardada.estado)" }
        if (@($guardada.servicios).Count -ne @(($cuerpo | ConvertFrom-Json).servicios).Count) { throw "$($archivo.Name): no se guardaron todos los servicios" }
        $script:convenios[$detalle.rut] = $borrador
        Write-Host "   $($detalle.rut) $($detalle.razonSocial): versión $($guardada.numero) VIGENTE, $(@($guardada.servicios).Count) servicios"
    }
}

Paso "4. Buscar el convenio vigente de 76.111.111-6 al $FechaVigencia" {
    $vigente = Llamar GET "/api/convenios/vigente?rut=76.111.111-6&fecha=$FechaVigencia"
    $esperado = $script:convenios['76.111.111-6']
    if ($vigente.version.versionId -ne $esperado.versionId) {
        # Si el script ya corrió antes sobre la misma base, gana el convenio más antiguo (RN-018).
        Write-Host "   (vigente $($vigente.version.versionId); hay $(@($vigente.candidatos).Count) candidatos: la base no estaba recién creada)"
    }
    if ($vigente.version.tcMinimo -ne 800) { throw "tcMinimo $($vigente.version.tcMinimo)" }
}

Write-Host "Prueba de punta a punta: todos los pasos OK." -ForegroundColor Green
