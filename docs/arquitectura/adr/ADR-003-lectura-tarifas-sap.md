# ADR-003 — Lectura de tarifas y artículos desde SAP

Estado: **Propuesto** (depende de PA-003, PA-007)
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
El "cuánto" (precios) no vive en AEXSA SYS (BR-012): vive en SAP como **kits** (encabezado) + **lista de artículos** (maestro) + **lista de precios** (objeto separado). Al seleccionar kit + lista de precios, SAP despliega los artículos amarrados con su precio por unidad; los artículos no están asociados al cliente, el valor lo aporta la lista de precios (RN-061). La lectura se confirmó **viable por API de consulta** (solo lectura, solo activos/vigentes), pero falta construirla y confirmar el objeto/query exacto (PQ-025, PA-003). La condición de pago y la lista de precios del cliente viven en la ficha del socio de negocio de SAP (RN-065), identificable por RUT (PA-007, BR-020).

El motor de tarifas del Satélite (ADR-006) necesita estos precios, pero el cálculo debe ser **determinístico y reproducible**: no puede depender de una llamada en vivo que devuelva un precio distinto cada vez.

## Decisión
Leer de SAP por una **API de solo consulta** detrás del puerto `ISapCatalogoPort`, y **materializar un snapshot versionado** de artículos y listas de precios dentro del Satélite para que el cálculo sea reproducible.

1. **Puerto `ISapCatalogoPort`:** `ObtenerArticulos()`, `ObtenerListaPrecios(listaId)`, `ObtenerKit(kitId)`, `ObtenerSocioNegocio(rut)`. Adaptador por Agunsa Connect (mismo gobierno que ADR-002).
2. **Sincronización programada** (p. ej. diaria) que trae artículos + listas de precios activos y los guarda como **`TarifaSnapshot` con versión y fecha de vigencia** en PostgreSQL. El cálculo de una operación **fija la versión del snapshot** usada (RNF-01, REQ-005, REQ-011).
3. **Identificación del cliente por RUT → `CardCode` de SAP** (mapeo por `FederalTaxID`, PA-007); se cachea la condición de pago y la lista de precios asociada (RN-065). La identidad en el Satélite es por **RUT**, deduplicando el ruido de nombres (BR-020).
4. El snapshot es de **lectura**; nunca se escribe hacia SAP por este canal (la escritura es ADR-002).
5. Las tarifas de **convenio** NO salen de SAP: viven en el Satélite (ADR-007). SAP aporta la **tarifa pública/kit spot** (RN-014) y el maestro de artículos/unidades.

## Alternativas consideradas
1. **Consultar SAP en vivo en cada cálculo.** Descartada: no es reproducible (el precio podría cambiar entre el cálculo y la auditoría), acopla el cálculo a la disponibilidad de SAP y multiplica el tráfico por Agunsa Connect.
2. **Duplicar manualmente las tarifas en el Satélite.** Descartada: desincroniza y recrea el problema de fuentes dispersas que el proyecto busca eliminar.
3. **Mantener las tarifas públicas solo en SAP y calcular allá.** Descartada: el Satélite debe armar la OFA valorizada (REQ-008) y detectar omisiones (REQ-007); necesita el precio localmente.

## Consecuencias
- El cálculo es reproducible: cada OFA guarda qué versión de snapshot de tarifa pública usó.
- Hay una ventana de desincronización (hasta la próxima sync); se mitiga con sync diaria + posibilidad de refresco manual y con la marca de versión en cada cálculo.
- Dependencia abierta: objeto/query exacto de SAP (PA-003) y mapeo RUT→CardCode (PA-007). Hasta confirmarse, el adaptador usa un stub con datos de ejemplo.
- La composición concreta de cada kit por ruta aún debe exportarse de SAP (PQ-025, pendiente menor del levantamiento); el modelo ya la soporta como `Kit → artículos`.

## Reglas / requerimientos relacionados
REQ-005, REQ-004, RN-014, RN-061, RN-032, RN-065, BR-012, BR-020, PQ-025, PQ-019, PA-003, PA-007.
