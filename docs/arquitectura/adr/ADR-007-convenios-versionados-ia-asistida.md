# ADR-007 — Convenios versionados con extracción asistida por IA

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
Decisión de negocio cerrada (no reabrir): **el convenio vigente vive en el Satélite** como fuente autoritativa (RN-071, REQ-012), reemplazando SharePoint/correos/carpetas (BR-001, BR-019). El comercial lo ingresa por **lectura asistida por IA** desde PDF/Word (extrae servicios, tarifas, unidades, moneda, TC mínimo, días libres, vigencia) **o manual**, con **confirmación humana antes de quedar vigente** (RN-071). El convenio tiene validez 1 año, se ajusta por correo como complemento, y puede haber varias versiones; manda la vigente y se conserva el histórico (RN-021, RN-037, RN-043, REQ-004). La IA **nunca fija un monto** (principio): extrae y propone; un humano confirma.

## Decisión
Modelar el **convenio como entidad versionada de primera clase en el Satélite**, con extracción asistida por IA como **adaptador opcional** detrás de un puerto, y **confirmación humana obligatoria** antes de que una versión quede vigente.

1. **Modelo versionado:** `Convenio` (cliente por RUT, comercial) → `VersionConvenio` (número de versión, vigencia desde/hasta, estado `BORRADOR|VIGENTE|VENCIDA|REEMPLAZADA`, origen) → `ServicioConvenio` (servicio, tarifa, unidad, moneda) + condiciones (TC mínimo, días libres). Un cálculo guarda la **versión de convenio aplicada** (ADR-006).
2. **Puerto `IExtraccionConvenioPort`** (IA, opcional): `ExtraerConvenio(documento) → PropuestaConvenio{ campos + score de confianza }`. Adaptador contra un servicio de IA (p. ej. extracción de documentos). Devuelve una **propuesta con umbral de confianza**, nunca una versión vigente.
3. **Confirmación humana:** toda `PropuestaConvenio` entra como `VersionConvenio` en estado `BORRADOR`; el comercial/analista revisa y confirma. Solo entonces pasa a `VIGENTE` (RN-071). Ingreso manual sigue el mismo flujo sin la extracción.
4. **Vigencia y alertas:** el Satélite marca la versión vigente (la más reciente por reglas de RN-043) y **alerta el vencimiento** al cumplirse 1 año (RN-021, REQ-004). Conserva el histórico completo (REQ-012).
5. **Complementos por correo:** un ajuste llega como **complemento versionado** del convenio (nueva `VersionConvenio` o delta), no sobreescribe (RN-037, REQ-004).
6. **Multi-convenio por BL:** cuando aplica más de uno, el Satélite **propone el más antiguo** y permite override comercial registrado (RN-018, REQ-004).
7. **Documento origen** (PDF/Word/correo) se guarda en S3 y se referencia desde la versión (trazabilidad del origen del monto).

## Alternativas consideradas
1. **Seguir con SharePoint/correos como fuente y solo indexar.** Descartado: contradice la decisión de negocio (RN-071) y mantiene la dispersión que causa el problema.
2. **Que la IA deje el convenio vigente automáticamente si la confianza es alta.** Descartado: viola el principio de confirmación humana y el de "la IA no fija montos"; un error de extracción se volvería tarifa aplicada.
3. **Sobrescribir la versión al ajustar por correo.** Descartado: rompe la auditabilidad (RNF-01) y la reconstrucción histórica; se versiona siempre.

## Consecuencias
- El Paso 2/3 consulta una fuente estructurada, versionada y auditable, no correos.
- La IA acelera la carga pero el humano es responsable del dato vigente (riesgo de error de extracción contenido por la confirmación).
- Cumplimiento de datos personales (BR-021/PA-005) aplica al almacenamiento de convenios con datos de cliente.

## Reglas / requerimientos relacionados
REQ-004, REQ-012, RN-071, RN-015, RN-018, RN-021, RN-022, RN-023, RN-035, RN-037, RN-043, BR-001, BR-011, BR-019.
