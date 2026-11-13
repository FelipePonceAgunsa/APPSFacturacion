# Formato de los archivos del catálogo de SAP (Fase A)

Versión: 1.0 · Fecha: 2026-11-04
Para la tarea: **1.1b Importación del catálogo SAP por archivo** (ADR-017).
Endpoints: etiqueta `Catalogo` del `openapi.yaml`.

## Para qué sirven
En la Fase A no existe la API de SAP. El equipo de SAP exporta cuatro archivos y el Satélite los importa a un `catalogo_snapshot` versionado. El motor usa ese snapshot para la **tarifa pública** (directo sin convenio, ítems fuera de convenio, clientes sin convenio), el **código de artículo SAP** de cada línea y el **tipo de cambio**.

Tú no esperas a esos archivos para empezar: genera archivos de ejemplo con este formato y construye el importador contra ellos. Cuando SAP entregue los reales, solo cambia el contenido.

## Reglas comunes a los cuatro archivos
- CSV en **UTF-8**, separador **punto y coma** (`;`).
- La **primera fila** trae los nombres de columna exactos de abajo, en minúsculas.
- Fechas `AAAA-MM-DD`. Decimales con **punto**. Los montos llevan moneda explícita (`CLP`, `USD` o `UF`).
- RUT con puntos y guion (`76.123.456-7`).
- Un archivo por tabla. Nombre sugerido: `A1_articulos_AAAAMMDD.csv`, `A2_listas_precios_AAAAMMDD.csv`, `A3_kits_AAAAMMDD.csv`, `A4_tipo_cambio_AAAAMMDD.csv`.

## Validación al importar
El importador **no activa nada solo**. Valida y devuelve un resultado (`ResultadoImportacion`) que una persona confirma:
- Columnas obligatorias presentes y con el tipo correcto.
- Sin duplicados por clave (se indica la fila).
- Un artículo inactivo en un kit o en una lista se informa como advertencia.
- Todo `codigo_articulo` de A2 y A3 debe existir en A1.
- Moneda dentro de los valores permitidos.
- Si hay errores, la importación queda `RECHAZADA` con la lista de fila, columna y mensaje.

Al confirmar se crea una **nueva versión** del catálogo. El cálculo guarda la versión que usó (memoria de cálculo), así que nunca se pisa una versión anterior.

---

## A1 — Artículos de servicio (obligatorio)
Tabla destino: artículos del `catalogo_snapshot`.

| Columna | Tipo | Obligatoria | Ejemplo |
|---|---|---|---|
| `codigo_articulo` | texto | Sí (clave) | SRV-REC-001 |
| `descripcion` | texto | Sí | Recepción contenedor |
| `unidad_medida` | texto | Sí | CTR |
| `activo` | S/N | Sí | S |
| `grupo_articulo` | texto | No | SERVICIOS |

Solo artículos de tipo servicio. Si SAP trae otros tipos, deben venir filtrados.

Ejemplo:
```
codigo_articulo;descripcion;unidad_medida;activo;grupo_articulo
SRV-REC-001;Recepción contenedor;CTR;S;SERVICIOS
SRV-DES-001;Despacho contenedor;CTR;S;SERVICIOS
SRV-VIS-001;Visación documental;BL;S;SERVICIOS
```

## A2 — Listas de precios (obligatorio)
Tabla destino: `tarifa_snapshot`.

| Columna | Tipo | Obligatoria | Ejemplo |
|---|---|---|---|
| `codigo_lista` | texto | Sí (clave, junto con artículo y vigencia) | LP-PUBLICA |
| `nombre_lista` | texto | Sí | Lista pública |
| `codigo_articulo` | texto | Sí | SRV-REC-001 |
| `precio` | decimal | Sí | 17000.00 |
| `moneda` | CLP, USD o UF | Sí | CLP |
| `vigente_desde` | fecha | Sí | 2026-01-01 |
| `vigente_hasta` | fecha | No | |

Debe incluir la lista pública (la de los kits "spot") y las listas de convenio que existan.

Ejemplo:
```
codigo_lista;nombre_lista;codigo_articulo;precio;moneda;vigente_desde;vigente_hasta
LP-PUBLICA;Lista pública;SRV-REC-001;17000.00;CLP;2026-01-01;
LP-PUBLICA;Lista pública;SRV-VIS-001;23.00;USD;2026-01-01;
```

## A3 — Kits y su composición (obligatorio)
Tablas destino: `sap_kit` y `sap_kit_articulo`.

| Columna | Tipo | Obligatoria | Ejemplo |
|---|---|---|---|
| `codigo_kit` | texto | Sí | SPOT-DIRECTO |
| `nombre_kit` | texto | Sí | SPOT contenedor directo |
| `codigo_articulo` | texto | Sí | SRV-VIS-001 |
| `cantidad` | decimal | Sí | 1 |
| `ruta` | texto | No | Directo sin convenio |
| `activo` | S/N | Sí | S |

Una fila por artículo del kit. **El kit es referencia, no entrada del cálculo** (ADR-017): se muestra como "kit sugerido" en la OFA y se marca por línea si está en el kit o se agrega a mano. El motor sigue armando las líneas con sus reglas.

Ejemplo:
```
codigo_kit;nombre_kit;codigo_articulo;cantidad;ruta;activo
SPOT-DIRECTO;SPOT contenedor directo;SRV-VIS-001;1;Directo sin convenio;S
SPOT-DIRECTO;SPOT contenedor directo;SRV-SEC-001;1;Directo sin convenio;S
```

## A4 — Tipo de cambio (obligatorio)
Tabla destino: `tipo_cambio`.

| Columna | Tipo | Obligatoria | Ejemplo |
|---|---|---|---|
| `fecha` | fecha | Sí (clave, junto con moneda) | 2026-11-03 |
| `moneda` | USD o UF | Sí | USD |
| `valor` | decimal | Sí | 812.40 |

Últimos 30 días. El tipo de cambio de un día también puede ingresarse a mano (`POST /catalogo/tipo-cambio`). La fuente preferida es el mismo que usa SAP al emitir, para que la prefactura coincida con la factura. Quién es responsable del dato diario sigue abierto (ADR-017, pendientes).

Cada valor guarda su fuente (`API`, `ARCHIVO` o `MANUAL`) y **gana el de SAP**:
- El valor manual solo se ingresa si falta el de SAP para ese día. Si ya existe el de SAP, la API responde 409 `TIPO_CAMBIO_DE_SAP_EXISTE`.
- Si después llega el de SAP para un día que tenía valor manual, lo reemplaza y el cambio queda en la auditoría.
- Los cálculos ya hechos no cambian, porque la memoria de cálculo guarda el valor que usó.

## A5 — Socios de negocio (deseable, no bloquea la Fase A)
No se importa en la Fase A. Sirve para mapear RUT→código de cliente SAP (PA-007) y traer condición de pago y lista de precios por cliente (RN-065). Se define en la Fase B (tarea 3.2). El tipo `SOCIOS_NEGOCIO` ya existe en la API, pero responde 400 `TIPO_NO_HABILITADO` hasta entonces.

---

## Carga completa y cargas parciales
- **Carga completa (`COMPLETO`):** A1, A2 y A3 (y A4 si se quiere) en **un mismo envío**, cada uno en su propia parte del formulario (`articulos`, `listasPrecios`, `kits`, `tipoCambio`). La validación cruzada (todo artículo de A2 y A3 existe en A1) se hace sobre ese envío. La **primera carga** del catálogo debe ser completa, o al menos traer A1.
- **Carga parcial (un solo archivo):** parte de la versión vigente. Al confirmar, la versión nueva copia desde ella lo que no vino. Por ejemplo, si solo llega A2, la versión nueva copia los artículos y los kits de la vigente. Así cada versión queda completa y el cálculo nunca encuentra un precio sin su artículo.

---

## Datos de prueba
- Genera los archivos de ejemplo con **códigos y precios inventados**. Nunca pegues datos reales de clientes ni de SAP en el repositorio.
- Incluye casos de error a propósito (artículo duplicado, moneda inválida, artículo inexistente en A1) para probar la validación.
- Incluye un caso de tipo de cambio **menor al piso** de un convenio de ejemplo, para el caso T3 del motor.
