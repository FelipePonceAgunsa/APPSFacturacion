# Prueba de punta a punta de la etapa 1 contra la API local en modo abierto (rol Administrador).
# Datos inventados de datos-prueba/. Uso, con la base recreada (db/recrear-con-migracion.ps1) y la API corriendo:
#   & .\datos-prueba\e2e-etapa1.ps1 *>&1
#   & .\datos-prueba\e2e-etapa1.ps1 -Api http://localhost:5228 *>&1
# Se detiene en el primer paso que falla e indica cuál (código de salida 1).
# Pasos 1 a 4: catálogo y convenios (bloques 2 y 3). Pasos 5 a 14: operaciones y OFA (bloques 4 y 5).
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

# --- Bloques 4 y 5: operaciones y OFA ---
# POST/PATCH con curl.exe para leer el código HTTP también en 4xx (Invoke-RestMethod de PowerShell 5.1 lanza error).
function Enviar([string]$metodo, [string]$ruta, [string]$json) {
    $entrada = [System.IO.Path]::GetTempFileName()
    $salida = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($entrada, $json, $utf8)
    $codigo = & curl.exe -s -o $salida -w '%{http_code}' -X $metodo -H 'Content-Type: application/json; charset=utf-8' --data-binary "@$entrada" "$Api$ruta"
    $cuerpo = [System.IO.File]::ReadAllText($salida, $utf8)
    Remove-Item $entrada, $salida
    [pscustomobject]@{ Codigo = [int]$codigo; Cuerpo = $(if ($cuerpo) { $cuerpo | ConvertFrom-Json } else { $null }) }
}
function Evento([string]$archivo) { [System.IO.File]::ReadAllText((Join-Path $raiz "eventos\$archivo"), $utf8) }
$script:operacion = $null
$script:ofa = $null
Paso '5. Enviar el evento de AEXSA v3.0 de ejemplo 01 (con convenio) → 201' {
    $r = Enviar POST '/api/aexsa/eventos' (Evento '01-impo-indirecto-con-convenio.json')
    if ($r.Codigo -ne 201 -or $r.Cuerpo.resultado -ne 'APLICADO') { throw "HTTP $($r.Codigo): $($r.Cuerpo | ConvertTo-Json -Compress)" }
    $script:operacion = $r.Cuerpo.operacionId
    $op = Llamar GET "/api/operaciones/$script:operacion"
    if (-not $op.conConvenio) { throw "la operación debía encontrar el convenio de $($op.rut)" }
    Write-Host "   operación $script:operacion · $($op.rutaClasificada) · convenio versión $($op.convenio.numero) · pendientes: $(@($op.pendientes).Count)"
}
Paso '6. Repetir el mismo evento → 409 YA_RECIBIDO' {
    $r = Enviar POST '/api/aexsa/eventos' (Evento '01-impo-indirecto-con-convenio.json')
    if ($r.Codigo -ne 409 -or $r.Cuerpo.codigo -ne 'YA_RECIBIDO') { throw "HTTP $($r.Codigo) $($r.Cuerpo.codigo)" }
}
Paso '7. Evento de visado presencial → 400 ENTIDAD_NO_HABILITADA' {
    $r = Enviar POST '/api/aexsa/eventos' (Evento '07-visado-presencial.json')
    if ($r.Codigo -ne 400 -or $r.Cuerpo.codigo -ne 'ENTIDAD_NO_HABILITADA') { throw "HTTP $($r.Codigo) $($r.Cuerpo.codigo)" }
}
Paso '8. Calcular la OFA y ver líneas con memoria de cálculo' {
    $script:ofa = Llamar POST "/api/operaciones/$script:operacion/ofa"
    foreach ($l in $script:ofa.lineas) {
        if (@($l.memoria.reglasAplicadas).Count -eq 0) { throw "línea $($l.tipoServicioId) sin reglas en la memoria" }
        if ($l.memoria.versionConvenio -ne 1) { throw "línea $($l.tipoServicioId) sin versión de convenio en la memoria" }
        Write-Host ("   {0,3} {1,-28} {2,8} x {3,10} {4} = {5,10} CLP  [{6}]" -f $l.tipoServicioId, $l.descripcionServicio, $l.cantidad, $l.tarifa, $l.moneda, $l.montoClp, (($l.memoria.reglasAplicadas | ForEach-Object regla) -join ', '))
    }
    if ($script:ofa.totalClp -ne 93000) { throw "total $($script:ofa.totalClp), se esperaba 93000" }
}
Paso '9. Agregar un ítem a mano → 201 AGREGADA_MANUAL' {
    $r = Enviar POST "/api/ofa/$($script:ofa.ofaId)/lineas" (@{ tipoServicioId = 44; cantidad = 1; motivo = 'Movilización extra pedida por el cliente (e2e)' } | ConvertTo-Json)
    if ($r.Codigo -ne 201 -or $r.Cuerpo.origen -ne 'AGREGADA_MANUAL') { throw "HTTP $($r.Codigo): $($r.Cuerpo | ConvertTo-Json -Compress)" }
    Write-Host "   ítem 44 por $($r.Cuerpo.montoClp) CLP, agregado por $($r.Cuerpo.agregadaPor)"
}
Paso '10. Ajustar un monto con motivo → memoria con el usuario del ajuste' {
    $linea = $script:ofa.lineas | Where-Object tipoServicioId -eq 42
    $sinMotivo = Enviar POST "/api/ofa/$($script:ofa.ofaId)/lineas/$($linea.lineaId)/ajuste" (@{ nuevoMonto = 14000; motivo = '' } | ConvertTo-Json)
    if ($sinMotivo.Codigo -ne 400) { throw "sin motivo debía responder 400 y respondió $($sinMotivo.Codigo)" }
    $r = Enviar POST "/api/ofa/$($script:ofa.ofaId)/lineas/$($linea.lineaId)/ajuste" (@{ nuevoMonto = 14000; motivo = 'Descuento comercial acordado (e2e)' } | ConvertTo-Json)
    if ($r.Codigo -ne 200) { throw "HTTP $($r.Codigo): $($r.Cuerpo | ConvertTo-Json -Compress)" }
    $ajuste = $r.Cuerpo.memoria.ajusteManual
    if ($ajuste.usuario -ne 'admin.local@zarpe.local') { throw "usuario del ajuste: $($ajuste.usuario)" }
    Write-Host "   $($ajuste.valorAnterior) → $($ajuste.valorNuevo) por $($ajuste.usuario): $($ajuste.motivo)"
}
Paso '11. Resolver los pendientes que bloquean la operación 01, si hay' {
    $op = Llamar GET "/api/operaciones/$script:operacion"
    $bloqueantes = @($op.pendientesDetalle | Where-Object { -not $_.resueltoEn -and $_.bloqueaValidacion })
    if ($bloqueantes.Count -eq 0) { Write-Host '   sin pendientes que bloqueen'; return }
    $decisiones = @($bloqueantes | Where-Object exigeDecision | ForEach-Object { @{ tipo = $_.tipo; valor = $(if ($_.tipo -eq 'VARIOS_CONVENIOS_VIGENTES') { $op.convenio.convenioId } elseif ($_.tipo -eq 'INSPECCION_RECHAZADA') { 'CONTINUAR' } else { 'FACTURAR' }) } })
    $r = Enviar PATCH "/api/operaciones/$script:operacion" (@{ motivo = 'Resuelto en la prueba e2e'; decisiones = $decisiones } | ConvertTo-Json -Depth 5)
    if ($r.Codigo -ne 200) { throw "HTTP $($r.Codigo): $($r.Cuerpo | ConvertTo-Json -Compress)" }
}
Paso '12. Validar la OFA → valida = true (lista para digitar en SAP)' {
    $v = Llamar POST "/api/ofa/$($script:ofa.ofaId)/validar"
    if (-not $v.valida) { throw "faltantes: $($v.faltantes -join ' | ')" }
    $ofa = Llamar GET "/api/ofa/$($script:ofa.ofaId)"
    Write-Host "   OFA $($ofa.estado), total $($ofa.totalClp) CLP, kit sugerido: $(if ($ofa.kitSugerido) { $ofa.kitSugerido.codigoKit } else { 'ninguno' })"
}
Paso '13. Enviar a SAP → 409 ENVIO_SAP_DESHABILITADO' {
    $r = Enviar POST "/api/ofa/$($script:ofa.ofaId)/enviar" '{}'
    if ($r.Codigo -ne 409 -or $r.Cuerpo.codigo -ne 'ENVIO_SAP_DESHABILITADO') { throw "HTTP $($r.Codigo) $($r.Cuerpo.codigo)" }
}
Paso '14. Directo sin convenio en STI y en DP World: tres conceptos y refacturación en 0 en DP World' {
    foreach ($caso in @(@{ Archivo = '02-directo-sti-sin-convenio.json'; RefacturacionCero = $false }, @{ Archivo = '03-directo-dpworld-sin-convenio.json'; RefacturacionCero = $true })) {
        $r = Enviar POST '/api/aexsa/eventos' (Evento $caso.Archivo)
        if ($r.Codigo -ne 201) { throw "$($caso.Archivo): HTTP $($r.Codigo)" }
        $ofa = Llamar POST "/api/operaciones/$($r.Cuerpo.operacionId)/ofa"
        $ids = (@($ofa.lineas | ForEach-Object tipoServicioId) | Sort-Object) -join ','
        if ($ids -ne '45,46,47') { throw "$($caso.Archivo): conceptos $ids" }
        $refacturacion = ($ofa.lineas | Where-Object tipoServicioId -eq 47).montoClp
        if (($refacturacion -eq 0) -ne $caso.RefacturacionCero) { throw "$($caso.Archivo): refacturación $refacturacion" }
        Write-Host "   $($caso.Archivo): conceptos $ids, refacturación $refacturacion CLP, total $($ofa.totalClp) CLP"
    }
}
Write-Host "E2E etapa 1: OK" -ForegroundColor Green
