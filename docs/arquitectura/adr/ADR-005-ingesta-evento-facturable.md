# ADR-005 — Ingesta del evento facturable desde AEXSA SYS

Estado: **Propuesto** (depende de PA-006)
Fecha: 2026-10-26
Decide: Arquitecto de Solución

> **Actualización 2026-11 (tras la revisión del código de AEXSA SYS):**
> 1. **Mecanismo.** Se adopta un esquema mixto: AEXSA escribe el evento en una **tabla de salida en la misma transacción** que la aprobación del visado; un proceso lo envía por **push** (`POST /aexsa/eventos`) con reintentos; y expone un `GET` de solo lectura sobre esa tabla para que el Satélite **concilie**. Ya no es "pull por defecto". El trigger en la base sigue descartado.
> 2. **Señal.** El disparador es el botón **"Aprueba"** de "Respuesta pre-visado": el visador lo presiona tras revisar los documentos. El evento se emite también en `PREAPROBADO_CONVENIO`: las operaciones con convenio no pasan por la fase Facturación de AEXSA y esa es la única señal que dan.
> 3. **Dónde vive la selección de "qué facturar".** En el Satélite: la bandeja de operaciones es la lista de pendientes de facturar. AEXSA no tiene hoy esa lista; su pantalla "Facturas por cobrar" muestra facturas ya emitidas pendientes de cobro, y el estado `PENDIENTE_FACTURA` no tiene filas. La fuente es una entidad explícita (el evento y la operación del Satélite), no el estado del visado.
> 4. **Ticket 1811 (facturación pendiente que bloquea retiros).** Queda separado: es control de retiros. En la Fase B podría consumir el estado de la operación del Satélite.
> 5. **Identidad.** El 1,9 % de los documentos tiene más de una solicitud de visado; el Satélite no valida una OFA si hay otra solicitud activa del mismo documento. *(Revisado 2026-11-13: el documento se identifica por `bls[].id`; ver punto 7.)*
> 6. **Entrega en dos etapas** en AEXSA (visado aprobado, anulación y `GET`; luego actualizaciones).
> Pendiente: fecha de AEXSA, autenticación de la llamada y si pasa por Agunsa Connect (PA-006, PA-002). Contrato en `contratos-integracion.md` §2.
>
> **Actualización 2026-11-13 (ajustes del equipo de AEXSA, contrato v3.0):**
> 7. **Origen e identidad.** El evento trae `entidad` (`VISADO_VIRTUAL` hoy; `VISADO_PRESENCIAL` después) y `operacionIdAexsa` pasa a ser un arreglo de IDs (uno en visado virtual, varios posibles en presencial). La idempotencia es `entidad` + `operacionIdAexsa` + `version`. Se eliminó `documentoCargaId`: los documentos llegan en `bls` (`id` y `numero`) y el documento compartido se detecta por `bls[].id`. `VISADO_PRESENCIAL` queda fuera de la etapa 1 hasta definir su identidad `[POR CONFIRMAR]`.
> 8. **DIN contra papeleta.** `papeletas` trae solo IDs, así que el Satélite ya no hace el contraste del punto 4 de la decisión: lo hace el visador en AEXSA antes de aprobar. `[POR CONFIRMAR]` con negocio (REQ-001).
> 9. **Aforo e inspecciones.** Se eliminó `bajoAforo`; el aforo llega en `inspecciones`, que suman `resultado`. Se eliminaron `dus` y `cantidadDus` en esta entrega.

## Contexto
El disparador del ciclo es la **aprobación del visado** por el visador en AEXSA SYS (RN-028, REQ-001). Hay dos mundos: con convenio se factura posterior al despacho (tope 7 días); carga suelta y sin convenio, en la visación (RN-046). AEXSA SYS **no publica eventos** hoy: la operación entra a "Facturación" cambiando de estado (`PREAPROBADO_* → FACTURA_EMITIDA`) en la BD (RN-028, BR-004). No hay un evento sistémico que alimente al Satélite (BR-004). Volumen ~2.428 documentos/mes (RNF-04).

## Decisión
Ingerir el evento facturable mediante un **puerto `IEventoFacturablePort`** con dos modos de adaptador, prefiriendo **push** si AEXSA SYS puede emitirlo, y **pull (polling) como opción por defecto** dado que el legacy no publica eventos.

1. **Puerto `IEventoFacturablePort`** que entrega un `EventoFacturable` normalizado (tipo de carga, tipo de retiro, cliente/consignatario, puerto, servicios asociados, DIN/DUS), base de REQ-001.
2. **Adaptador pull (por defecto):** el Satélite consulta periódicamente una **vista o API de solo lectura** sobre AEXSA SYS que liste operaciones recién aprobadas (estado de visado = aprobado) no ingeridas aún. Cada operación se marca como ingerida por su identificador para no reprocesar (idempotencia por `operacionId`).
3. **Adaptador push (preferido si viable):** si TI-AEXSA agrega un webhook/cola al aprobar el visado, el Satélite expone `POST /api/aexsa/eventos` y el pull queda como respaldo. (PA-006)
4. **Validaciones de entrada (REQ-001):** al recibir el evento, el Satélite contrasta **DIN contra papeleta**; si hay discrepancia, marca la operación **no facturable** y la devuelve a corrección (RN-001). Clasifica el **momento de facturación** según convenio/sin convenio (RN-046): con convenio, retiene hasta cumplir el despacho (tope 7 días).
5. **Antirrebote:** una operación genera un único evento facturable vigente; reingestas actualizan, no duplican.

## Alternativas consideradas
1. **Trigger en la BD de AEXSA SYS que llame al Satélite.** Descartada por defecto: acopla al esquema interno del legacy y es frágil; se mantiene como alternativa solo si TI-AEXSA la implementa y gobierna.
2. **Que el usuario dispare manualmente la ingesta.** Descartada: reintroduce el paso manual que el proyecto elimina (REQ-001); se deja solo como acción de reproceso.
3. **Lectura directa de tablas del legacy sin vista intermedia.** Descartada: expone el Satélite a cambios internos de AEXSA; se exige una vista/API estable como contrato.

## Consecuencias
- El Satélite arranca el ciclo sin exigir que AEXSA SYS cambie (pull), y mejora a push si el legacy lo permite.
- La idempotencia por operación evita doble facturación ante reintentos del polling.
- Dependencia abierta: existencia de la vista/API de lectura y, deseablemente, del webhook (PA-006).

## Reglas / requerimientos relacionados
REQ-001, REQ-002, RN-001, RN-028, RN-046, BR-004, BR-022, PA-006.
