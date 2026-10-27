# ADR-010 — Modelo de datos del núcleo, auditoría y trazabilidad

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
El valor del sistema es la **auditabilidad** (RNF-01) y la **trazabilidad extremo a extremo** operación → OFA → factura (folio) → pago (RNF-02). Cada monto debe reconstruirse: qué ítems, qué convenio/tarifa y versión, qué TC, qué ajustes (REQ-011). El cliente se identifica por **RUT** (BR-020). El catálogo de servicios se normaliza desde `MasterData.TipoServicio` (192 servicios, unidades sin normalizar, códigos truncados — BR-014, RN-032). La cuadratura por **folio** une toda la cadena (RN-029).

## Decisión
Un **modelo de dominio centrado en la Operación** con entidades versionadas y una **memoria de cálculo inmutable por OFA**, en PostgreSQL vía EF Core, con auditoría append-only.

### Entidades del núcleo (resumen)
- **Cliente** — identidad por `Rut`; mapeo a `CardCode` de SAP (condición de pago, lista de precios). Deduplicado por RUT (BR-020).
- **Operacion** — el evento facturable ingerido (ADR-005): tipo de carga, tipo de retiro (directo/indirecto), con/sin convenio, puerto, DIN/DUS, ruta clasificada (REQ-002), momento de facturación (RN-046). Referencia al `operacionId` de AEXSA.
- **ServicioPrestado** — servicios de la operación (reefer, inspección, almacenaje, complementarios), base del cruce de omisiones (ADR-009).
- **Convenio / VersionConvenio / ServicioConvenio** — versionado (ADR-007).
- **TipoServicio** — catálogo normalizado desde `MasterData.TipoServicio`, clave `TipoServicioID`, **unidad normalizada** (enum), `Habilitado` (BR-014, RN-032).
- **TarifaSnapshot** — snapshot versionado de artículos/listas de precios de SAP (ADR-003).
- **ReglaConfigurable** — reglas de nivel 2 versionadas (ADR-008).
- **Ofa** — la Orden de Facturación: cabecera + `LineaOfa` (ítem, cantidad, unidad, tarifa, moneda, monto, destinatario). Soporta **múltiples destinatarios** (ADR-011). Estados: `BORRADOR|VALIDADA|ENVIADA|EMITIDA|EMITIDA_SIN_CONFIRMACION|CONCILIADA`.
- **Destinatario / AsignacionCobro** — múltiples destinatarios con sus conceptos/montos (repartido, ADR-011, RN-077/RN-050).
- **MemoriaCalculo** — **inmutable**: por cada `LineaOfa`, qué regla/versión, qué versión de convenio, qué versión de snapshot de tarifa, qué TC, qué ajuste manual. Es la reconstrucción del "por qué" (REQ-011, RNF-01).
- **Factura** — folio, tipo DTE, montos, XML del DTE (conciliación, ADR-004). Une por **folio** (RN-029).
- **Pago** — estado de pago conciliado desde SAP; soporta pago que agrupa varias facturas y de varios clientes (REQ-010, RN-067).
- **EventoAuditoria** — log append-only transversal (quién, cuándo, qué, valor anterior/nuevo).
- **OutboxMessage** — integración saliente (ADR-002).

### Reglas de diseño del modelo
1. **Versionado con vigencia** (desde/hasta) en convenios, tarifas y reglas configurables; un cálculo fija las versiones usadas.
2. **Append-only para auditoría:** `MemoriaCalculo` y `EventoAuditoria` no se actualizan ni borran; un cambio genera un nuevo registro.
3. **Folio como clave de cuadratura** operación↔OFA↔factura↔pago (RNF-02).
4. **Identidad por RUT**, no por nombre (BR-020).
5. **Unidades normalizadas** a un enum; el texto original de AEXSA se conserva como referencia (BR-014).
6. **Documentos** (convenio PDF/Word, DTE XML) en S3, referenciados por ruta; la BD guarda metadatos.

## Alternativas consideradas
1. **Event sourcing del dominio completo.** Descartado para la 1ª etapa: sobredimensionado para el volumen (RNF-04); la combinación "estado actual + MemoriaCalculo/EventoAuditoria append-only" da la auditabilidad requerida con menor complejidad.
2. **Mutar las líneas de la OFA al reajustar.** Descartado: perdería la reconstrucción histórica; se versiona y se registra el ajuste.
3. **Modelo centrado en la factura (como el legacy).** Descartado: el centro es la **operación** (hay operaciones sin factura aún, repartidas, con complementarias); la factura es un resultado.

## Consecuencias
- Cualquier monto histórico es reconstruible aunque cambien tarifas/convenios después.
- La cuadratura por folio sostiene la detección de omisiones y los KPIs futuros (REQ-011).
- Mayor volumen de datos de auditoría (append-only); se gestiona con retención y particionado si hace falta.
- El cumplimiento de datos personales (BR-021/PA-005) aplica a Cliente/Convenio/Factura: cifrado en reposo (RDS) y en tránsito, y control de acceso.

## Reglas / requerimientos relacionados
REQ-011, REQ-003, REQ-010, RNF-01, RNF-02, RNF-04, RN-029, RN-032, RN-065, RN-067, RN-077, BR-013, BR-014, BR-020, BR-021.
