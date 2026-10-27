# ADR-012 — Flujo de convenios: carga directa en el Satélite (reemplazo de SharePoint)

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución
Decisión de negocio asociada: Opción B, confirmada por el usuario 2026-10-26.

## Contexto
Hoy los convenios **no están en SAP**: la gran mayoría vive en **SharePoint**. El comercial negocia, modifica el contrato y lo sube ahí; la vigencia se infiere del correlativo del nombre del archivo y conviven múltiples versiones (RN-043, BR-011, BR-019). SharePoint no es una fuente estructurada ni autoritativa (BR-001): no hay estado vigente/vencido confiable, ni tarifas estructuradas, ni trazabilidad de quién cambió qué.

La decisión de negocio ya cerrada (RN-071, ADR-007) es que **el convenio vigente vive en el Satélite**. Faltaba definir **cómo se hace el tránsito** desde el SharePoint vivo. Entre las opciones evaluadas (A: vigilar SharePoint; B: carga directa en el Satélite; C: híbrido), el negocio eligió la **Opción B**.

## Decisión
**El comercial carga el convenio directamente en el Satélite.** SharePoint deja de ser el flujo de trabajo de convenios y queda, a lo sumo, como **archivo histórico de solo lectura**. El Satélite es el único punto donde se crea, versiona y activa un convenio.

1. **Punto único de carga:** el comercial sube el documento del convenio (PDF/Word) a través de la UI del Satélite (pantalla de convenios). No hay más subida a SharePoint como parte del proceso de facturación.
2. **Lectura asistida por IA + confirmación humana** (ADR-007): al cargar, el Satélite extrae servicios, tarifas, unidad, moneda, TC mínimo, días libres y vigencia como **propuesta** (`version_convenio` en estado `BORRADOR`); el comercial/analista revisa y **confirma** antes de que pase a `VIGENTE`. También se admite ingreso/edición manual.
3. **Versionado nativo:** cada carga o ajuste genera una **nueva versión** con su vigencia y estado; el Satélite determina la vigente y conserva el histórico (RN-071, RN-021). Elimina la ambigüedad del "correlativo en el nombre del archivo" (RN-043/BR-011).
4. **Documento origen en S3:** el PDF/Word cargado se guarda en S3 y se referencia desde la versión (`version_convenio.documento_s3`), para trazar el origen del monto.
5. **Autoría y auditoría:** se registra quién cargó, quién confirmó y cuándo (`evento_auditoria`), cerrando la brecha de "no se sabe quién cambió qué" de SharePoint (BR-019).
6. **Ajustes por correo:** un ajuste puntual (porteo, días libres, inspección) se registra como **complemento versionado** del convenio en el Satélite (RN-037), no como un archivo suelto.
7. **Migración inicial (una vez):** los convenios vigentes que hoy están en SharePoint se cargan al Satélite en un proceso de arranque (carga asistida por IA, lote), con confirmación humana. A partir de ahí, SharePoint no vuelve a ser origen.

## Alternativas consideradas
1. **Opción A — el Satélite vigila SharePoint** (Microsoft Graph API, detección de cambios) y lo lee automáticamente. Descartada por el negocio: mantiene a SharePoint como origen (y con él su desorden de versiones y falta de trazabilidad), y acopla el Satélite a un repositorio no estructurado.
2. **Opción C — híbrido/transición** (empezar vigilando SharePoint y migrar a carga directa). Descartada: añade complejidad de doble flujo; el negocio prefiere el cambio directo a carga en el Satélite.
3. **Dejar los convenios en SharePoint y solo indexarlos.** Descartada: contradice RN-071 (fuente autoritativa en el Satélite) y no resuelve la vigencia ni la trazabilidad.

## Consecuencias
- **Fuente única y autoritativa** del convenio vigente, versionada y auditable, desde el día uno (resuelve BR-001/BR-011/BR-019).
- **Cambio de hábito del comercial:** deja de subir a SharePoint y pasa a cargar en el Satélite. Requiere acompañamiento/capacitación (gestión del cambio, no desarrollo).
- **Migración inicial** de los convenios vigentes de SharePoint al Satélite: esfuerzo acotado y por única vez (carga asistida por IA + confirmación).
- SharePoint queda como **histórico de solo lectura**; no se integra técnicamente (no hay dependencia de Graph API en el flujo productivo).
- No cambia el modelo de datos (ya previsto en `modelo-datos.md`: `convenio`, `version_convenio`, `servicio_convenio`, `documento_s3`); refuerza el uso de `origen` y la auditoría.

## Reglas / requerimientos relacionados
REQ-004, REQ-012, RN-071, RN-015, RN-021, RN-037, RN-043, BR-001, BR-011, BR-019. Refina ADR-007 (concreta el punto de entrada del convenio).
