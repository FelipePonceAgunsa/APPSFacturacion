# Datos de prueba (inventados)

Todo lo de esta carpeta es inventado: códigos, precios, RUT y nombres. Nunca se copian datos reales de clientes ni de SAP.
RUT de ejemplo (dígito verificador módulo 11): `76.111.111-6`, `77.222.222-K`, `78.333.333-3`.

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

## Configuración del motor en Development
`Motor:ConceptosDirecto` de `Satelite.Api/appsettings.Development.json` usa los IDs 42 a 47 de estas semillas
(`[SUPUESTO: IDs de datos de prueba]`). `appsettings.json` no trae valores: cada ambiente fija los suyos.
