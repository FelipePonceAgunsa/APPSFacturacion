# Plan de implementación — Satélite de Facturación AEXSA

Versión: 1.0
Fecha: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Base: `design.md` v1.0, ADR-001…ADR-011, `requirements.md` v1.1, `docs/arquitectura/plan-entregas.md` v1.0.

> Principio de secuencia: construir primero el **dominio determinístico** (motor de tarifas, convenios, reglas, OFA) con **adaptadores simulados** de SAP/AEXSA, para avanzar sin esperar a PA-001/PA-002 (la escritura en SAP y el contrato de Agunsa Connect). Los adaptadores reales se enchufan cuando esas PA se confirmen, sin tocar el dominio.
>
> **Priorización por valor de negocio** (ver `plan-entregas.md`): las fases se agrupan en entregas que dan valor sin depender de terceros (**Fase A**) y entregas que eliminan la digitación y cierran el círculo pero dependen de terceros (**Fase B**). En la primera fase operativa el Satélite entrega la **prefactura valorizada** y el facturador la **digita en SAP como hoy**; la escritura automática llega en la Entrega 4.
>
> | Entrega (plan-entregas.md) | Fases de este documento | Dependencia externa |
> |---|---|---|
> | **E1 — Convenios automatizados** | Fase 0 + 1.2 + 4.1/4.2 | ninguna |
> | **E2 — Factura con valores ajustados (prefactura)** | 1.1, 1.3–1.6, 2.1–2.3 | ninguna |
> | **E3 — Detección de omisiones** | 2.4 + 4.3 | ninguna |
> | **E4 — Integración automática con SAP** | 3.2, 3.3, 3.4 | PA-001/002/003 |
> | **E5 — Cierre del lazo y pagos** | 3.1, 3.5, 3.6 + Fase 5 | PA-004/006/008/005 |
>
> Fase A = E1+E2+E3 (valor temprano, sin terceros). Fase B = E4+E5 (depende de terceros, se estima aparte).
>
> Cada tarea registra: archivos/artefactos a generar, requerimientos y ADR que cubre, y pendientes. El estado se marca `[ ]` pendiente / `[~]` en progreso / `[x]` hecho. Las tareas con dependencia externa llevan **(bloqueada por PA-xxx)** y se construyen contra stub.

---

## Fase 0 — Fundaciones

- [ ] **0.1 Scaffolding de la solución** (.NET 10, hexagonal)
  - Proyectos: `Satelite.Dominio`, `Satelite.Aplicacion`, `Satelite.Infraestructura`, `Satelite.Api`, `Satelite.Worker` (Outbox), `Satelite.Tests`.
  - Referencias: Dominio sin dependencias; Aplicación→Dominio; Infraestructura→Aplicación; Api/Worker→todo.
  - Cubre: ADR-001. Verificación: compila y corre un test vacío.
- [ ] **0.2 Base de datos y EF Core** (PostgreSQL)
  - Migración inicial; convención de entidades versionadas y tablas append-only.
  - Cubre: ADR-010. Verificación: migración aplica sobre PostgreSQL local/contenedor.
- [ ] **0.3 CI/CD y entorno**
  - GitHub Actions (build + test), ECR, contenedor; secretos en Secrets Manager (no en config, BR-016).
  - Cubre: ADR-001, §7 del diseño. Verificación: pipeline verde.

## Fase 1 — Núcleo determinístico (sin dependencias externas)

- [ ] **1.1 Catálogo de servicios normalizado** (`TipoServicio`)
  - Importar desde `MasterData.TipoServicio` (192); normalizar unidades a enum; clave `TipoServicioID`; filtrar `Habilitado`.
  - Cubre: REQ (catálogo), RN-032, BR-014. ADR-010.
- [ ] **1.2 Convenios versionados** (modelo + CRUD)
  - `Convenio/VersionConvenio/ServicioConvenio`; estados y vigencia; multi-versión con histórico; vigente = más reciente (RN-043); alerta de vencimiento (RN-021); multi-convenio → más antiguo + override (RN-018).
  - Cubre: REQ-004, REQ-012, RN-015/018/021/037/043. ADR-007. (Extracción IA → tarea 4.2.)
- [ ] **1.3 Motor de tarifas — pipeline base**
  - `ContextoCalculo` inmutable; reglas puras: resolución de tarifa (convenio→pública con marca excepción, RN-006/014), cantidad×tarifa por unidad, USD→CLP con piso (RN-022).
  - Cubre: REQ-005. ADR-006. Verificación: pruebas unitarias por regla.
- [ ] **1.4 Motor de tarifas — reglas de almacenaje y recargos**
  - Días calculados − días libres (RN-023/051); no cobro bajo aforo (RN-052); recargos 15%/90%/sobrepeso/fuera de horario (RN-024); "mayor m³/ton" (RN-048).
  - Cubre: REQ-005. ADR-006. Verificación: casos del levantamiento como tests.
- [ ] **1.5 Unidad de cobro DUS/BL y repartido**
  - Driver de cobro por servicio (BL/DUS/CTR/TON/M3); `CantidadDus` en Operación (RN-047); OFA multi-destinatario `AsignacionCobro` (RN-050/077); RUT de la DIN (RN-057).
  - Cubre: REQ-003, REQ-005. ADR-011.
- [ ] **1.6 MemoriaCalculo y auditoría**
  - Append-only por `LineaOfa`: reglas/versiones, versión de convenio, versión de snapshot, TC, ajustes; `EventoAuditoria` transversal.
  - Cubre: REQ-011, RNF-01. ADR-010. Verificación: reconstruir un cálculo desde la memoria en un test.

## Fase 2 — Capa configurable y OFA

- [ ] **2.1 Reglas configurables (nivel 2)**
  - `ReglaConfigurable` condición→acción, versionada; evaluada en el pipeline; CRUD con auditoría.
  - Cubre: REQ-006, RN-070. ADR-008.
- [ ] **2.2 Ajuste manual asistido (nivel 3) + promoción**
  - Ajuste de concepto/monto/excepción con usuario/fecha/motivo/valor ant.-nuevo; detección de recurrencia; promoción a regla configurable.
  - Cubre: REQ-006, RN-070, BR-023. ADR-008.
- [ ] **2.3 Armado y validación de la OFA**
  - Cabecera + `LineaOfa`; validación de completitud (destinatario, ítems con tarifa o marcados, recargos/días resueltos); bloqueo si incompleta.
  - Cubre: REQ-008, RN-059/060. ADR-010.
- [ ] **2.4 Detección de omisiones (cruce determinístico)**
  - Cruce prestado↔valorizado; alerta de faltantes (reefer, inspecciones, complementarios, forwarder); propone ítem, decisión registrada; necesidad de complementaria tardía (RN-056).
  - Cubre: REQ-007, RN-068, BR-003/013. ADR-009. (IA propositiva → tarea 4.3.)

## Fase 3 — Integraciones (puerto + stub primero, adaptador real al confirmar PA)

- [ ] **3.1 Ingesta del evento facturable**
  - Puerto `IEventoFacturable`; adaptador **pull** a vista/API de AEXSA (stub primero); validación DIN↔papeleta (RN-001); clasificación de ruta (REQ-002) y momento (RN-046); idempotencia por operación.
  - Cubre: REQ-001, REQ-002. ADR-005. **(real bloqueada por PA-006.)**
- [ ] **3.2 Lectura de tarifas desde SAP + snapshot**
  - Puerto `ISapCatalogo`; sync programada → `TarifaSnapshot` versionado; mapeo RUT→CardCode, condición de pago/lista de precios (RN-065).
  - Cubre: REQ-005 (fuente), RN-014/061. ADR-003. **(real bloqueada por PA-003/PA-007; stub con datos de ejemplo.)**
- [ ] **3.3 Outbox + despachador de la OFA hacia SAP**
  - `OutboxMessage` en la misma transacción que la OFA; worker con reintentos/backoff; idempotencia por `OfaId`+versión; `EMITIDA_SIN_CONFIRMACION` + alerta; log request/response/error; reproceso desde UI.
  - Cubre: REQ-008, REQ-009, RNF-07. ADR-002. **(adaptador real bloqueado por PA-001/PA-002; se prueba con `AgunsaConnectSapAdapter` stub.)**
- [ ] **3.4 Recepción de folio/DTE y conciliación**
  - Endpoint callback de folio; recibe DTE XML; concilia calculado↔DTE por folio; cierra vínculo operación↔factura.
  - Cubre: REQ-009, RN-029/076. ADR-002, ADR-004. **(bloqueada por PA-002.)**
- [ ] **3.5 Retorno a AEXSA SYS**
  - Puerto `IAexsaRetorno`; notifica folio para habilitar check de salida (RN-072); reutiliza canal de conciliación; idempotente por folio.
  - Cubre: REQ-009, RN-072/074. ADR-004. **(bloqueada por PA-004/PA-008.)**
- [ ] **3.6 Conciliación de pagos**
  - Recibe estado de pago por folio desde SAP; soporta pago agrupado multi-factura/multi-cliente (RN-067); alerta de impago; saldo a favor/devolución (registro).
  - Cubre: REQ-010, RN-063/067/069. ADR-004, ADR-010.

## Fase 4 — Capacidades asistidas por IA (propone, nunca fija monto)

- [ ] **4.1 Puerto de IA y gobierno de confianza**
  - `IExtraccionConvenio` / `IDeteccionOmisiones`; umbral de confianza; toda salida de IA es propuesta con confirmación humana.
  - Cubre: principios, RN-070. ADR-007/008/009.
- [ ] **4.2 Extracción asistida de convenios**
  - Lee PDF/Word/correo → `PropuestaConvenio` (servicios, tarifas, unidad, moneda, TC mínimo, días libres, vigencia) en estado BORRADOR; confirmación humana → VIGENTE; documento origen en S3.
  - Cubre: REQ-012, RN-071. ADR-007.
- [ ] **4.3 Detección de omisiones asistida por IA**
  - Propone faltantes por patrones/anomalías sobre datos de facturación/operación (no de pago, RN-068); confirmación humana.
  - Cubre: REQ-007. ADR-009.

## Fase 5 — Frontend, trazabilidad y cierre

- [ ] **5.1 Frontend Angular**
  - Pantallas: convenios (ingreso/confirmación), facturación (OFA, ajustes, omisiones), recaudación (conciliación/impagos). Por CloudFront+WAF.
  - Cubre: REQ-004/006/008/010/012. ADR-001.
- [ ] **5.2 Histórico auditable y reconstrucción**
  - Vista de operación cerrada: reconstruir cálculo completo desde `MemoriaCalculo`; asociar complementarias/notas de crédito.
  - Cubre: REQ-011. ADR-010.
- [ ] **5.3 Cuadraturas y alertas**
  - Paneles de cuadratura (AEXSA↔Satélite↔OFA↔SAP↔pago); alertas de desbalance, OFA sin folio, convenios por vencer.
  - Cubre: RNF-02, RNF-07, principios.
- [ ] **5.4 Seguridad y cumplimiento de datos personales**
  - Cifrado en reposo/tránsito; control de acceso por rol; revisión de ciberseguridad antes de producción.
  - Cubre: BR-021, §7 del diseño. **(bloqueada por PA-005.)**

---

## Hitos y dependencias externas

Agrupados por entrega de valor (ver `plan-entregas.md`):

| Entrega | Contenido | Valor para negocio | Depende de |
|---------|-----------|--------------------|-----------|
| **E1 — Convenios automatizados** | Fase 0 + convenios versionados + extracción IA | Fuente única del convenio vigente; fin de la búsqueda en correos | nada externo |
| **E2 — Factura con valores ajustados** | Catálogo + motor determinístico + capa configurable + OFA (prefactura para digitar) | Menos errores y notas de crédito; cálculo automático | nada externo |
| **E3 — Detección de omisiones** | Cruce prestado↔facturado + IA propositiva | Recupera cobros que hoy se pierden | nada externo |
| **E4 — Integración automática con SAP** | Lectura real + Outbox + escritura OFA + folio | Elimina la digitación manual en SAP | **PA-001, PA-002, PA-003** |
| **E5 — Cierre del lazo y pagos** | Ingesta real + retorno a AEXSA + conciliación de pagos + frontend + seguridad | Check de salida automático; trazabilidad op→factura→pago | **PA-004, PA-005, PA-006, PA-008** |

> **Fase A = E1 + E2 + E3:** valor operativo temprano. Es lo que el desarrollo con IA acelera (dominio, cálculo, convenios, pruebas); **no depende de terceros**. El Satélite ya entrega la prefactura valorizada que el facturador digita como hoy.
>
> **Fase B = E4 + E5:** elimina la digitación y cierra el círculo. **Depende de terceros** (proveedor SAP, Agunsa Connect, AEXSA SYS, ciberseguridad) y **no se acelera**; su plazo se estima y gestiona aparte, según el principio de no prometer aceleración donde no la hay.

## Próximos pasos antes de codificar
1. Confirmar PA-001/PA-002 con TI/SAP/arquitectura corporativa (define el adaptador de escritura).
2. Confirmar PA-003/PA-006 (vías de lectura de SAP e ingesta de AEXSA).
3. Cerrar la única regla de monto de negocio abierta: STI vs DPW (PQ-006, con Francisco).
