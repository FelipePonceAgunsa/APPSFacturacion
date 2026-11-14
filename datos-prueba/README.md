# Datos de prueba (inventados)

Todo lo de esta carpeta es inventado: códigos, precios, RUT y nombres. Nunca se copian datos reales de clientes ni de SAP.
RUT de ejemplo (dígito verificador módulo 11): `76.111.111-6`, `77.222.222-K`, `78.333.333-3`.

## Cargar todo de una vez (sin PowerShell)
Con la API corriendo (F5 en Visual Studio), doble clic en `cargar-datos-demo.bat`, o bien
`python datos-prueba/cargar_datos_demo.py [--api http://localhost:5228]`.
Carga el catálogo A1 a A4, vincula los servicios 42 a 52, crea y confirma los dos convenios, envía los eventos
01 a 06, 09 y 10 (no los 07 y 08, que son inválidos a propósito) y calcula la OFA de cada operación.
Se puede repetir: salta el catálogo si ya hay uno vigente, los convenios que ya existen y los eventos ya recibidos.
Requiere las semillas `db/semillas/*.sql` en la base; si faltan, el script lo avisa.

`POST /api/aexsa/eventos` exige HTTP Basic (credencial provisoria de AEXSA SYS, PA-002). Para cargar eventos:
1. En Visual Studio, clic derecho en `Satelite.Api` → *Administrar secretos de usuario*, y agregar
   `"Aexsa:Basic:Usuario": "aexsa-local"` y `"Aexsa:Basic:Clave": "<una clave inventada>"`. Reiniciar la API.
2. En la ventana de cmd, antes del script: `set ZARPE_AEXSA_USUARIO=aexsa-local` y `set ZARPE_AEXSA_CLAVE=<la misma clave>`.
Sin eso el paso 5 responde 401 y el script explica qué falta. La clave nunca va en el código ni en `appsettings.json`.

## catalogo/
Archivos A1 a A4 con el formato de `docs/desarrollo/formato-archivos-catalogo-sap.md`.

| Archivo | Contenido |
|---|---|
| `A1_articulos_20261101.csv` | Un artículo por servicio de `db/semillas/01-tipo-servicio.sql` (42 a 52) y `SRV-OLD-001` inactivo |
| `A2_listas_precios_20261101.csv` | `LP-PUBLICA` (la lista pública, `Catalogo:ListaPublica`) en CLP, USD y un precio en UF; `LP-CONV-EJEMPLO` |
| `A3_kits_20261101.csv` | `SPOT-DIRECTO` (ruta "Directo sin convenio") y `SPOT-INDIRECTO` ("Indirecto sin convenio") |
| `A4_tipo_cambio_20261101.csv` | 30 días de USD (2026-10-14 a 2026-11-12) y dos días de UF. El **2026-10-26 vale 700**, bajo el piso de 800 del convenio de `76.111.111-6` (caso T3) |
| `error_A1_duplicado.csv` | Artículo repetido (`DUPLICADO`) y `activo = X` (`FORMATO`) |
| `error_A2_moneda_invalida.csv` | Moneda `EUR` (`MONEDA_INVALIDA`) y decimal con coma (`FORMATO`) |
| `error_A2_articulo_inexistente.csv` | Para cargar como `LISTAS_PRECIOS` sobre la versión 1: `SRV-NOEXISTE-001` (`ARTICULO_NO_EXISTE`) y `SRV-OLD-001` inactivo (advertencia) |

Vínculo servicio ↔ artículo sugerido para las pruebas: 42 `SRV-REC-001`, 43 `SRV-DES-001`, 44 `SRV-MOV-001`,
45 `SRV-VIS-001`, 46 `SRV-SEC-001`, 47 `SRV-REF-001`, 48 `SRV-ALM-001`, 49 `SRV-SAG-001`, 50 `SRV-USO-001`,
51 `SRV-DOC-001`, 52 `SRV-MAN-001`.

Carga por la API (PowerShell 5.1, API en modo abierto en `http://localhost:5228`):
```powershell
curl.exe -s -F tipo=COMPLETO -F articulos=@datos-prueba/catalogo/A1_articulos_20261101.csv `
  -F listasPrecios=@datos-prueba/catalogo/A2_listas_precios_20261101.csv -F kits=@datos-prueba/catalogo/A3_kits_20261101.csv `
  -F tipoCambio=@datos-prueba/catalogo/A4_tipo_cambio_20261101.csv http://localhost:5228/api/catalogo/importaciones
curl.exe -s -X POST http://localhost:5228/api/catalogo/importaciones/<importacionId>/confirmar
```

## convenios/
Cuerpos de `POST /api/convenios` (formulario manual, ADR-012). Nacen en BORRADOR y hay que confirmarlos.

| Archivo | Contenido |
|---|---|
| `convenio-76111111-6.json` | Vigencia 2026; tarifas CLP (42, 43, 48) y USD (45); `tcMinimo` 800 (piso del caso T3 con el TC de 700 del 2026-10-26); `diasLibres` 10 |
| `convenio-77222222-K.json` | Vigencia 2026-06-01 a 2027-05-31; tarifas CLP (42, 49) y USD (46); `tcMinimo` 820; `diasLibres` 5 |

## eventos/
Mensajes de AEXSA con el contrato v3.0 (`contratos-integracion.md` §2), para `POST /api/aexsa/eventos` o el alta manual
`POST /api/operaciones`. Visados entre el 2026-10-26 y el 2026-10-29 (el TC del catálogo de ejemplo cubre esas fechas).
| Archivo | Caso | Respuesta esperada |
|---|---|---|
| `01-impo-indirecto-con-convenio.json` | 76.111.111-6 (convenio), indirecto, 1 contenedor con 15 días, aforo ADU-FIS del 21 al 23 (2 días) y SAG aprobada | 201; OFA de 93.000 CLP (almacenaje 15 − 10 libres − 2 aforo = 3 días; visación con el piso de 800) |
| `02-directo-sti-sin-convenio.json` | Directo en el terminal 1 (STI), sin convenio | 201; OFA con visación, security fee y refacturación con valor |
| `03-directo-dpworld-sin-convenio.json` | Directo en el terminal 2 (DP World), sin convenio | 201; refacturación en 0 |
| `04-expo-sin-cantidad-dus.json` | Exportación sin cantidad de DUS | 201; pendiente `CANTIDAD_DUS` |
| `05-tipo-retiro-null.json` | Sin tipo de retiro, sentido ni terminal; aforo sin terminar; almacenaje sin cantidad | 201; pendientes `TIPO_RETIRO`, `SENTIDO`, `TERMINAL_SIN_MAPEAR`, `CANTIDAD_SERVICIO`, `DIAS_AFORO`, `INSPECCION_NO_FINALIZADA` |
| `06-version-2-de-01.json` | Versión 2 de 01 (agrega movilización y la fecha de despacho) | 200; si la OFA está en BORRADOR o VALIDADA se recalcula |
| `07-visado-presencial.json` | `VISADO_PRESENCIAL` con 2 IDs | 400 `ENTIDAD_NO_HABILITADA` |
| `08-virtual-dos-ids.json` | `VISADO_VIRTUAL` con 2 IDs | 400 `INVALIDO` |
| `09-bulto.json` | Carga suelta | 201; `facturable = false` (fuera de alcance) |
| `10-comparte-bl-con-01.json` | Comparte el BL 184520 con 01; facturador 77.222.222-K | 201; pendiente `DOCUMENTO_CON_VARIAS_SOLICITUDES` en ambas |
## e2e-etapa1.ps1
Prueba de punta a punta contra la API local en modo abierto. Con la base recién creada y la API levantada **después**
de recrearla (el script de la base corta las conexiones abiertas):
```powershell
& .\db\recrear-con-migracion.ps1 *>&1
dotnet run --project Fuentes\AEXSA.ZARPE.BACKEND.NET\Satelite.Api --launch-profile http   # en otra consola
& .\datos-prueba\e2e-etapa1.ps1 *>&1
```
Pasos: 1 importa y confirma el catálogo COMPLETO (y comprueba que un archivo con error queda RECHAZADA);
2 vincula los servicios 42 a 52; 3 crea y confirma los dos convenios y los lee VIGENTE con `GET /convenios/{id}`;
4 busca el convenio vigente de `76.111.111-6` al 2026-10-26. Si un paso falla, dice cuál y sale con código 1.

## Configuración del motor en Development
`Motor:ConceptosDirecto` de `Satelite.Api/appsettings.Development.json` usa los IDs 42 a 47 de estas semillas
(`[SUPUESTO: IDs de datos de prueba]`). `appsettings.json` no trae valores: cada ambiente fija los suyos.
