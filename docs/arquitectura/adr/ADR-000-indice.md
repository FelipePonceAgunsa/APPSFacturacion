# Índice de ADRs — Satélite de Facturación AEXSA

Última actualización: 2026-10-26

Cada ADR documenta una decisión de arquitectura relevante con sus alternativas, consecuencias y las reglas/requerimientos del levantamiento que la originan. Formato: contexto → decisión → alternativas consideradas → consecuencias → estado.

| ADR | Título | Estado | Resuelve / depende de |
|-----|--------|--------|------------------------|
| [ADR-001](ADR-001-estilo-arquitectonico.md) | Estilo arquitectónico y stack | Aceptado | Estándar corporativo; base de todo |
| [ADR-002](ADR-002-integracion-sap-agunsa-connect.md) | Integración con SAP B1 vía Agunsa Connect (Outbox + adaptadores) | Propuesto | PQ-029, PQ-030, BR-027, BR-028, RN-076, PA-001, PA-002 |
| [ADR-003](ADR-003-lectura-tarifas-sap.md) | Lectura de tarifas y artículos desde SAP | Propuesto | PQ-025, RN-061, PA-003, PA-007 |
| [ADR-004](ADR-004-retorno-aexsa-sys.md) | Retorno de facturación a AEXSA SYS | Propuesto | PQ-032, RN-072, RN-074, PA-004, PA-008 |
| [ADR-005](ADR-005-ingesta-evento-facturable.md) | Ingesta del evento facturable desde AEXSA SYS | Propuesto | REQ-001, RN-028, BR-004, PA-006 |
| [ADR-006](ADR-006-motor-tarifas-determinista.md) | Motor de tarifas determinístico y versionado | Aceptado | REQ-005, RNF-03, principios |
| [ADR-007](ADR-007-convenios-versionados-ia-asistida.md) | Convenios versionados con extracción asistida por IA | Aceptado | REQ-004, REQ-012, RN-071, BR-001 |
| [ADR-008](ADR-008-capa-configurable-reglas.md) | Capa configurable para reglas no escritas | Aceptado | REQ-006, RN-070, BR-023 |
| [ADR-009](ADR-009-deteccion-omisiones.md) | Motor de detección de omisiones (IA propone, no decide) | Aceptado | REQ-007, RN-068, BR-013 |
| [ADR-010](ADR-010-modelo-datos-auditoria.md) | Modelo de datos del núcleo, auditoría y trazabilidad | Aceptado | REQ-011, RNF-01, RNF-02 |
| [ADR-011](ADR-011-facturacion-repartida-dus.md) | Facturación repartida y DUS como driver de cobro | Aceptado | REQ-003, REQ-005, RN-077, RN-047, BR-024, BR-030 |
| [ADR-012](ADR-012-flujo-convenios-carga-directa.md) | Flujo de convenios: carga directa en el Satélite (reemplaza SharePoint) | Aceptado | REQ-004, REQ-012, RN-071, BR-001, BR-011, BR-019 |
| [ADR-013](ADR-013-carga-suelta-letra-tarifaria.md) | Carga suelta y letra tarifaria (diseño anticipado, 2ª etapa) | Aceptado (construcción diferida) | RN-040, BR-018, BR-025, PQ-007 |
| [ADR-014](ADR-014-alcance-2a-etapa-restante.md) | Alcance restante de 2ª etapa: expo DUS, licitaciones, notas de crédito (diseño anticipado) | Aceptado (construcción diferida) | RN-034, RN-047, RN-069, BR-030, PQ-024 |
| [ADR-015](ADR-015-calculo-contenedor-directo-sti-dpw.md) | Cálculo del contenedor directo (STI vs DP World) | Aceptado | RN-078, RN-079, RN-007, PQ-006, BR-007 |
| [ADR-016](ADR-016-autenticacion-cognito-entra-id.md) | Autenticación y autorización: Cognito federado con Entra ID | Aceptado (detalle en PA-009) | REQ-006, REQ-011, BR-016, BR-021, PA-009 |

## Estados
- **Propuesto:** decisión tomada por el Arquitecto, pendiente de confirmación de un tercero (TI/SAP/seguridad). Ver la PA asociada.
- **Aceptado:** decisión cerrada, no depende de terceros; se puede construir.
- **Supersedido:** reemplazado por un ADR posterior (se indica cuál).
