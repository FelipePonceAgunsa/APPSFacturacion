# ADR-011 — Facturación repartida y DUS como driver de cobro

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
Dos **capacidades nuevas** que el legacy no tiene y que el negocio sí necesita (principio "manda el negocio", handoff §2.4):

- **Repartido:** el negocio reparte el cobro entre consignatario y forwarder (ej. ACL: recepción+despacho+security fee al consignatario, recepción+despacho al forwarder — RN-050). AEXSA SYS soporta **un único facturador** por operación (`ClienteFacturadorID` + `TipoClienteFacturador`), sin repartido; el forwarder se asigna con rol `EMISOR_BL` (RN-077, BR-024, verificado en BD). El campo se usa en 0,03% de operaciones; las variantes llegan por correo (BR-024/BR-026).
- **DUS en expo:** en exportación la unidad de cobro es el **DUS** (se cobra por cantidad de DUS presentados), no el BL; en importación es BL/ton/m³ (RN-047). AEXSA SYS no modela el DUS como driver de cobro (solo `REFDUS` aduanero, sin cantidad — BR-030, verificado en BD). Nota de alcance: buena parte del cobro por DUS es carga suelta expo (BR-018, 2ª etapa); el modelo lo soporta y se prioriza según alcance.

## Decisión
Modelar ambas como capacidades de primera clase del Satélite, sin forzarlas al esquema del legacy.

### Repartido
1. La **OFA soporta múltiples destinatarios**: `Ofa → AsignacionCobro[]`, cada una con un `Destinatario` (por RUT) y el subconjunto de `LineaOfa` (conceptos/montos) que le corresponde (REQ-003, RN-050).
2. Por defecto, un único destinatario = consignatario de la DIN (RN-002). Instrucciones comerciales pueden cambiarlo a forwarder/embarcador o **repartir**; el **origen de la instrucción se registra** (hoy vive en correos — BR-024; el Satélite lo estructura).
3. El RUT del documento es el de la **declaración (DIN/DUS)**, no el de la papeleta (RN-057).
4. Cada `AsignacionCobro` genera su propia orden de venta hacia SAP (ADR-002): repartir ⇒ varias órdenes/facturas desde una operación.

### DUS como driver de cobro
5. **Unidad de cobro parametrizable por servicio:** el `TipoServicio` declara su unidad (BL, DUS, CTR, TON, M3, CTR-DIA…). En expo, los servicios cuyo driver es DUS se cobran por **cantidad de DUS** de la operación (RN-047).
6. **`CantidadDus` como atributo de la Operacion** (capacidad nueva; AEXSA no lo tiene): se captura en la ingesta o manualmente, y alimenta el motor de tarifas (ADR-006).
7. "El mayor paga" (m³ vs tonelada) se aplica donde el driver es volumen/peso (RN-048).

## Alternativas consideradas
1. **Un solo destinatario (como el legacy) y repartir con dos operaciones manuales.** Descartado: contradice el negocio (RN-050) y reintroduce trabajo manual y riesgo de omisión (BR-024).
2. **Modelar el DUS solo como referencia aduanera (como AEXSA).** Descartado: no permitiría cobrar por DUS (RN-047); manda el negocio.
3. **Esperar a 2ª etapa para ambas.** Repartido es FULL (1ª etapa) y se incluye; el DUS se modela ahora pero se **prioriza** su activación según si expo/LCL entra en 1ª etapa (BR-018).

## Consecuencias
- Una operación puede producir **varias facturas** (repartido); la trazabilidad por folio (ADR-010) las asocia a la misma operación.
- El motor de tarifas trata la unidad de cobro de forma uniforme (incluido DUS), sin casos especiales dispersos.
- Pendiente menor de negocio: mecánica fina de cantidad de DUS por operación (BR-030); el modelo ya la admite.

## Reglas / requerimientos relacionados
REQ-003, REQ-005, REQ-008, RN-047, RN-048, RN-050, RN-057, RN-077, BR-018, BR-024, BR-026, BR-030.
