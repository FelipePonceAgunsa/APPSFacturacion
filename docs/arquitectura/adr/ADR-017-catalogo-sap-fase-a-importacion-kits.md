# ADR-017 — Catálogo de SAP en la Fase A: importación por archivo, kits y vínculo servicio↔artículo

Estado: **Aceptado** para la Fase A · la sincronización por API sigue en la Fase B (ADR-003, PA-003)
Fecha: 2026-10-28
Decide: Arquitecto de Solución

## Contexto
En la Fase A el Satélite entrega la prefactura valorizada y el facturador la digita en SAP. En SAP el facturador elige un **kit** y una **lista de precios**, y SAP despliega los artículos amarrados con su precio por unidad (RN-061). Sin ver el kit, tendría que teclear línea por línea o elegirlo de memoria, que es el problema actual.

Además, el motor necesita datos de SAP para que la prefactura salga completa:
- **Tarifa pública** para el directo sin convenio (ADR-015), para ítems fuera de convenio (RN-006) y para clientes sin convenio.
- **Código de artículo SAP** de cada línea, para que el facturador la encuentre.
- **Tipo de cambio** para las tarifas en USD (RN-022). Ningún documento definía su origen.

Corrección a ADR-003: afirmaba que el modelo "ya soportaba Kit → artículos", pero el DDL no tenía tablas de kits ni vínculo servicio↔artículo.

La API de lectura (PA-003) no existe y depende de Agunsa Connect, por lo que no puede condicionar la Fase A.

## Decisión
1. **Importación por archivo.** El equipo de SAP exporta a Excel o CSV: artículos de servicio, listas de precios, kits con su composición y tipo de cambio. El Satélite los importa a un `catalogo_snapshot` versionado. Antes de activarse, el import se valida (formato, duplicados, artículos inactivos) y lo confirma una persona. Es un adaptador más del puerto `ISapCatalogoPort`; la API de la Fase B será otro adaptador, sin tocar el dominio.
2. **Kits como referencia, no como entrada del cálculo.** Se guardan en `sap_kit` y `sap_kit_articulo`, versionados con el snapshot. El motor sigue armando las líneas con sus reglas. La OFA muestra el **kit sugerido y la lista de precios**, y marca por línea si está en el kit o hay que agregarla a mano.
3. **Kit sugerido por ruta.** Se propone según la ruta clasificada (REQ-002) y el convenio. Si el cliente no tiene kit propio en SAP, se sugiere el de la ruta y se señalan las diferencias. `[SUPUESTO: la regla ruta→kit se afina con facturación al ver la exportación real]`.
4. **Vínculo servicio↔artículo** en `servicio_articulo_sap`. La IA propone por similitud de descripción y unidad; una persona confirma (Paso 3 del levantamiento). Una línea sin vínculo confirmado sale marcada "sin artículo SAP".
5. **Tipo de cambio** en la tabla `tipo_cambio`. La fuente preferida es el mismo que usa SAP al emitir, para que la prefactura coincida con la factura. En la Fase A se importa en el mismo archivo o lo ingresa facturación cada día. Falta definir quién es responsable del dato.
6. **Pantalla "Catálogo SAP"** de solo lectura, con artículos, precios, kits, vínculo servicio↔artículo y tipo de cambio, más la función de importar.

## Alternativas consideradas
1. **Esperar la API de SAP (PA-003).** Descartada: bloquearía la Fase A por una dependencia de terceros.
2. **Digitar las tarifas públicas a mano en el Satélite.** Descartada (ya en ADR-003): se desincroniza.
3. **No mostrar kits.** Descartada: el facturador perdería el atajo que hoy usa en SAP.
4. **Usar el kit como entrada del cálculo.** Descartada: el kit agrupa lo que se cobra "normalmente", pero lo que se factura depende de la operación, del convenio y de reglas que el kit no conoce.

## Consecuencias
- Aparece una **dependencia pequeña**: alguien del equipo de SAP exporta al inicio y cada vez que cambien los precios. No es un proveedor, pero la Fase A deja de ser 100% independiente.
- Riesgo de **precios desfasados** entre exportaciones. Se mitiga mostrando versión y fecha del catálogo en cada OFA.
- La calidad del vínculo servicio↔artículo depende del catálogo de AEXSA, hoy sin normalizar (BR-014).
- Suma trabajo a E2: del orden de 4 a 6 horas Kiro `[SUPUESTO]`. No se ajustó aún el cronograma ni la estimación.
- Cuando exista la API, el import por archivo queda como respaldo o carga manual.

## Pendientes
- PQ-025: exportación concreta del detalle de los kits por ruta.
- PQ-019: qué convenios ya tienen kit propio en SAP (el levantamiento dice que solo los más usados).
- Responsable del tipo de cambio diario.

## Reglas / requerimientos relacionados
REQ-005, REQ-008, RN-006, RN-014, RN-022, RN-061, RN-078, BR-014, PQ-019, PQ-025, PA-003. Corrige y complementa ADR-003; usa ADR-006 y ADR-015.
