# Preguntas de arquitectura — Satélite de Facturación AEXSA

Última actualización: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)

IDs correlativos PA-NNN, nunca se reutilizan. Son decisiones técnicas que requieren confirmación de TI/SAP/seguridad y que condicionan el diseño. Se diferencian de las PQ (negocio) del levantamiento: estas son del **CÓMO**.

Prioridad: Crítica (bloquea una integración o el cálculo) · Alta (afecta el diseño de un componente) · Media / Baja.

| ID | Pregunta | Prioridad | Depende de (levantamiento) | Dirigida a | ADR relacionado | Estado |
|----|----------|-----------|----------------------------|------------|-----------------|--------|
| PA-001 | ¿Cómo se construye el canal de **escritura** hacia SAP B1 (crear la orden de venta desde la OFA y devolver folio)? ¿Service Layer (REST/OData) de SAP B1, DI API, o desarrollo de un proveedor externo? ¿Qué objeto de negocio crea la orden de venta (`Orders`)? | Crítica | PQ-029 / BR-027 / RN-076 | TI / Proveedor SAP / Agunsa Connect | ADR-003 | Abierta |
| PA-002 | ¿Qué contrato y tecnología exige **Agunsa Connect** (protocolo, autenticación, formato de mensaje, SLA, patrón síncrono/asíncrono)? ¿Expone colas, REST, o un bus? | Crítica | PQ-030 / BR-028 | Arquitectura corporativa (Francisca, Luis Veloso, Ani; dueño Iván) | ADR-002 | Abierta |
| PA-003 | ¿La **API de lectura** de SAP (artículos + lista de precios + kits) se expone como consulta directa al Service Layer o como un servicio intermedio del proveedor? ¿Objeto/query exacto (`Items`, `PriceLists`, `ItemGroups`)? | Alta | PQ-025 | TI-SAP (Marlene Bozzo) | ADR-003 | Abierta |
| PA-004 | ¿El retorno a AEXSA SYS reutiliza el canal existente `API/SAP/GetFactura` (hoy SAP→AEXSA), se crea un endpoint nuevo en AEXSA SYS, o el Satélite escribe directo en la BD de AEXSA? | Alta | PQ-032 / RN-072 / RN-074 | TI-AEXSA (Ricardo) / Arquitectura | ADR-004 | Abierta |
| PA-005 | ¿Qué política de retención y cifrado aplica a los datos personales de clientes (nueva ley) y qué controles exige ciberseguridad antes de implementar? | Alta | BR-021 | Ciberseguridad / Legal | ADR-010 | Abierta |
| PA-006 | ¿El Satélite consume el **evento de visado** de AEXSA SYS por push (webhook/cola) o por pull (polling a una vista/API)? AEXSA SYS no publica eventos hoy. | Alta | REQ-001 / RN-028 / BR-004 | TI-AEXSA / Arquitectura | ADR-005 | Abierta |
| PA-007 | ¿Cómo se identifica la ficha del cliente en SAP (socio de negocio) desde el RUT del Satélite, para traer condición de pago y lista de precios? ¿El `CardCode` se mapea por RUT (`FederalTaxID`)? | Media | REQ-007 (NF) / RN-065 / BR-020 | TI-SAP | ADR-003 | Abierta |
| PA-008 | ¿El XML del DTE (software de facturación electrónica, no SAP) es accesible para el Satélite, o solo el número de documento vía integración? | Media | PQ-031 / RN-062 | TI (Ricardo) | ADR-004 | Abierta |

## Notas
- PA-001 y PA-002 son el bloqueante técnico principal (coinciden con el riesgo marcado en el handoff §3). El diseño se estructura para que el resto del Satélite avance **sin** depender de su resolución (ver ADR-002, patrón Outbox y adaptadores).
- Las respuestas a estas preguntas se incorporan como actualización de los ADR correspondientes, no reabren decisiones de negocio.
