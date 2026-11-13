# Plan de implementación — ZARPE - FACTURACIÓN

Versión: 1.1
Fecha: 2026-11-04
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Base: `design.md` v1.1, ADR-001…ADR-017, `docs/desarrollo/requerimientos.md`, `docs/desarrollo/reglas-negocio.md`.

> **Cambios de la v1.1 respecto de la v1.0 (decisiones del 2026-10-28 al 2026-11-04):**
> 1. Se agrega la tarea **0.4 Autenticación y roles** (ADR-016).
> 2. Los **convenios se ingresan por formulario manual** (tarea 1.2). La extracción con IA (4.1 y 4.2) pasa a **etapa opcional**.
> 3. La **ingesta del evento** se divide: la parte mínima (3.1a) entra en la **Fase A** y el adaptador real contra AEXSA SYS (3.1b) queda en la Fase B.
> 4. El **frontend** se divide: las pantallas de la Fase A (5.1a) y las de recaudación y cuadraturas (5.1b, Fase B).
> 5. Las capacidades de IA (4.x) y la propuesta de vínculo por IA (parte de 1.1b) son **opcionales**: el sistema funciona completo sin ellas.

> **Principio de secuencia:** construir primero el **dominio determinístico** (motor de tarifas, convenios, reglas, OFA) con **adaptadores simulados** de SAP y AEXSA, para avanzar sin esperar a PA-001/PA-002 (la escritura en SAP y el contrato de Agunsa Connect). Los adaptadores reales se enchufan cuando esas PA se confirmen, sin tocar el dominio.
>
> **Priorización por valor de negocio** (ver `docs/desarrollo/GUIA-DESARROLLADOR.md`): las fases se agrupan en entregas que dan valor sin depender de terceros (**Fase A**) y entregas que eliminan la digitación y cierran el círculo pero dependen de terceros (**Fase B**). En la Fase A el Satélite entrega la **prefactura valorizada** y el facturador la **digita en SAP como hoy**.
>
> | Entrega | Tareas de este documento | Dependencia externa |
> |---|---|---|
> | **E1 — Convenios** | 0.1–0.4, 1.2, 5.1a (convenios) | ninguna (Cognito: ver PA-009) |
> | **E2 — Factura con valores ajustados (prefactura)** | 1.1, 1.1b, 1.3–1.6, 2.1–2.3, 3.1a, 5.1a | archivos de catálogo de SAP (A1 a A4) |
> | **E3 — Detección de omisiones** | 2.4, 5.1a (omisiones) | ninguna |
> | **E4 — Integración automática con SAP** | 3.2, 3.3, 3.4 | PA-001, PA-002, PA-003 |
> | **E5 — Cierre del lazo y pagos** | 3.1b, 3.5, 3.6, 5.1b, 5.2, 5.3, 5.4 | PA-004, PA-005, PA-006, PA-008 |
>
> **Fase A = E1 + E2 + E3** (valor temprano, sin terceros). **Fase B = E4 + E5** (depende de terceros, se estima aparte).
>
> **Estados:** `[ ]` pendiente · `[~]` en progreso · `[x]` hecho. Las tareas con dependencia externa llevan **(bloqueada por PA-xxx)** y se construyen contra un simulador (stub). La etapa de IA (Fase 4) es **opcional**: no se construye salvo que el negocio la pida.
>
> **Registro de avance:** al terminar cada tarea, completar la tabla del final de este documento (archivos generados, pruebas, hallazgos y pendientes).

---

## Orden de ejecución recomendado

El orden por número de fase no es el orden óptimo. Este es el recomendado, pensado para entregar rebanadas completas y probables:

| Paso | Tareas | Qué queda funcionando | Por qué en este orden |
|---|---|---|---|
| 1 | 0.1, 0.2, 0.3 | Solución compila, base de datos migra, pipeline en verde | Base de todo |
| 2 | 1.3, 1.4, 1.5, 1.6 | Motor de tarifas con los 12 casos de prueba (T1 a T12) en verde | Es el núcleo del valor y no necesita infraestructura. Se prueba solo, con datos en memoria |
| 3 | 0.4 | Login con roles y API protegida | Todo lo que sigue necesita saber quién hace qué (auditoría) |
| 4 | 1.2 + pantallas de convenios (5.1a) | Se ingresa, confirma y versiona un convenio | Primera rebanada visible para el negocio (E1) |
| 5 | 1.1, 1.1b + pantalla de catálogo (5.1a) | Se importa el catálogo SAP por archivo y se vinculan servicios | Alimenta la tarifa pública y los artículos (E2) |
| 6 | 3.1a, 2.1, 2.2, 2.3 + pantallas de operaciones y OFA (5.1a) | De un evento a una prefactura validada y ajustable | Cierra la E2: el facturador ya puede digitar en SAP |
| 7 | 2.4 + pantalla de omisiones (5.1a) | Alerta de cobros omitidos | E3 |
| 8 | Pruebas de integración y de aceptación de la Fase A | Fase A lista para QA con negocio | Cierre de la Fase A |
| 9 | Fase B: 3.2, 3.3, 3.4, 3.1b, 3.5, 3.6, 5.1b, 5.2, 5.3, 5.4 | Escritura a SAP, folio, pagos, retorno a AEXSA | Contra simuladores hasta que los terceros entreguen |

---

## Fase 0 — Fundaciones

- [~] **0.1 Scaffolding de la solución** (.NET 10, hexagonal) — *hecho en `Fuentes/`, sin `Satelite.Worker` (etapa 2); falta CI (0.3) para cerrarla*
  - Proyectos: `Satelite.Dominio`, `Satelite.Aplicacion`, `Satelite.Infraestructura`, `Satelite.Api`, `Satelite.Worker` (Outbox), `Satelite.Tests`.
  - Referencias: Dominio sin dependencias; Aplicación→Dominio; Infraestructura→Aplicación; Api/Worker→todo.
  - Cubre: ADR-001. Verificación: compila y corre un test vacío.
- [x] **0.2 Base de datos y EF Core** (PostgreSQL) — *migración `Inicial` que ejecuta `db/init/01` + `02`; equivalencia probada con `pg_dump -s` (`db/recrear-con-migracion.ps1`)*
  - Migración inicial a partir de `docs/arquitectura/modelo-datos.md`; convención de entidades versionadas y tablas append-only.
  - Cubre: ADR-010. Verificación: migración aplica sobre PostgreSQL local o en contenedor.
- [ ] **0.3 CI/CD y entorno**
  - GitHub Actions (build + test), ECR, contenedor; secretos en Secrets Manager (no en config, BR-016).
  - Cubre: ADR-001, §7 del diseño. Verificación: pipeline verde.
- [~] **0.4 Autenticación y roles** (ADR-016) — *nueva. Validación del JWT, roles y política por defecto hechas; faltan el manejador de autenticación de prueba, las pruebas de integración y los valores de PA-009*
  - La API valida el JWT de Cognito: firma (JWKS), emisor, audiencia o `client_id`, y expiración. No emite tokens ni guarda credenciales.
  - Roles del Satélite: Comercial, Facturación, Recaudación, Administrador. Se aplican **en la API** con políticas de autorización, no solo ocultando menús.
  - Acciones que exigen rol explícito: confirmar convenio, ajuste manual, promover regla, reenviar OFA, importar y confirmar catálogo.
  - Un usuario del pool que no tenga rol del Satélite recibe 403. Un token emitido para otro app client se rechaza.
  - La identidad del token (`sub` y correo) se guarda como `usuario` en `evento_auditoria`.
  - **Para desarrollar sin esperar a PA-009:** autoridad y audiencia configurables; en pruebas, un manejador de autenticación de prueba que inyecta usuarios con rol.
  - Cubre: REQ-006, REQ-011, RNF-05, BR-016, BR-021. ADR-016. **Dependencia:** el app client del Satélite y los grupos de roles los entrega quien administra el pool (PA-009).
  - Verificación: pruebas de integración con tokens de prueba válidos, vencidos, de otra audiencia y sin rol.

## Fase 1 — Núcleo determinístico (sin dependencias externas)

- [ ] **1.1 Catálogo de servicios normalizado** (`TipoServicio`)
  - Importar desde `MasterData.TipoServicio` (192); normalizar unidades a enum; clave `TipoServicioID`; filtrar `Habilitado`.
  - Cubre: REQ (catálogo), RN-032, BR-014. ADR-010.
- [ ] **1.1b Importación del catálogo SAP por archivo** (Fase A) — *puente: se usa solo mientras SAP no entregue la API-1 (3.2). Las APIs las construye el equipo SAP; nosotros las especificamos en `solicitud-apis-sap.md`. Si la API-1 llega antes de probar la Fase A, se usa la API y el archivo queda solo para el análisis previo y como respaldo*
  - **Cada versión del catálogo es completa** (`modelo-datos.md` §5, DBA-3b): una importación parcial (por ejemplo, solo listas de precios) crea la versión nueva copiando desde la vigente los artículos, listas y kits que no vinieron. Antes de confirmar se valida que cada artículo de una lista o kit exista en la versión vigente (`ARTICULO_NO_EXISTE`). La primera versión debe traer artículos. Pruebas: importación solo de listas sobre una versión con artículos; lista con un artículo inexistente; primera importación sin artículos (rechazada).
  - **Paso previo: análisis de los datos de SAP.** Con la primera extracción, antes de construir el importador definitivo, revisar con el equipo SAP y facturación: campos reales y su significado, artículos duplicados o inactivos, cómo está armado un kit, listas de precios vigentes, moneda de cada precio y origen del tipo de cambio. Dejar el resultado en el registro de avance. Lo que se aprenda define también el contrato de la API-1 de la Fase B (`solicitud-apis-sap.md`).
  - Importar CSV exportado de SAP: artículos (A1), listas de precios (A2), kits con composición (A3) y tipo de cambio (A4). Formato en `docs/desarrollo/formato-archivos-catalogo-sap.md`.
  - Resultado: `catalogo_snapshot` versionado, con validación (formato, duplicados, artículos inactivos) y **confirmación humana** antes de activarse. Endpoints en el OpenAPI, etiqueta `Catalogo`.
  - Vínculo servicio↔artículo (`servicio_articulo_sap`): lo fija una persona. La sugerencia automática por coincidencia de descripción y unidad es opcional y solo propone; la IA es opcional (Fase 4).
  - Kit sugerido en la OFA por ruta. Pantalla "Catálogo SAP" (T3).
  - Cubre: REQ-005, REQ-008, RN-061, BR-014. ADR-017. **Dependencia menor:** el equipo de SAP entrega los archivos (PQ-025). No usa API.
  - Para desarrollar sin los archivos reales: generar archivos de ejemplo con el formato documentado.
- [ ] **1.2 Convenios versionados** (modelo + CRUD + formulario manual) — *ajustada*
  - Modelo `Convenio/VersionConvenio/ServicioConvenio`; estados `BORRADOR|VIGENTE|VENCIDA|REEMPLAZADA`; multi-versión con histórico; vigente = más reciente (RN-043); alerta de vencimiento al año (RN-021); multi-convenio → más antiguo + override registrado (RN-018).
  - **Ingreso por formulario manual, simple** (`POST /convenios`, `POST /convenios/{id}/versiones`). Solo lo que el motor necesita: RUT del cliente; vigencia desde/hasta; tipo de cambio mínimo (solo si hay tarifas en USD); días libres de almacenaje (solo si el convenio los tiene); tabla de servicios con servicio del catálogo, tarifa y moneda (**CLP o USD; UF no aplica**). La unidad de cobro sale del catálogo y no se digita. El comercial es opcional.
  - Sin importación por planilla: el volumen no lo justifica. Los servicios que no están en el convenio no se ingresan: el motor usa la tarifa pública y los marca como excepción (RN-006).
  - Una versión nace en BORRADOR; una persona con rol la confirma y pasa a VIGENTE. Nunca se edita una versión vigente: cada cambio es una versión nueva. Los ajustes por correo se registran como complemento versionado (RN-037).
  - Cubre: REQ-004, REQ-012, RN-015/018/021/037/043. ADR-007, ADR-012.
  - Verificación: pruebas de vigencia, reemplazo de versión, alerta de vencimiento y override.
- [x] **1.3 Motor de tarifas — pipeline base**
  - `ContextoCalculo` inmutable; reglas puras: resolución de tarifa (convenio→pública con marca excepción, RN-006/014), cantidad×tarifa por unidad, USD→CLP con piso (RN-022).
  - Cubre: REQ-005. ADR-006. Verificación: pruebas unitarias por regla (T1 a T4, T12).
- [x] **1.4 Motor de tarifas — reglas de almacenaje y recargos**
  - Días calculados − días libres (RN-023/051); no cobro bajo aforo (RN-052); recargos 15%/90%/sobrepeso/fuera de horario (RN-024); "mayor m³/ton" (RN-048).
  - Cubre: REQ-005. ADR-006. Verificación: casos T5 a T9.
- [~] **1.5 Unidad de cobro DUS/BL, repartido y directo STI/DP World** — *hecha la parte del motor (driver DUS/BL/CTR/TON/M3, Regla 0 con T10, T10b y T10c); falta el repartido manual (`AsignacionCobro`), que no tiene endpoint en el OpenAPI*
  - Driver de cobro por servicio (BL/DUS/CTR/TON/M3); `CantidadDus` en Operación por captura manual (RN-047); OFA multi-destinatario `AsignacionCobro` (RN-050/077); RUT de la DIN (RN-057).
  - Regla 0 del directo sin convenio: tres conceptos (visación, security fee y refacturación); refacturación con valor en STI y en cero en DP World (RN-078/079, ADR-015). Casos T10, T10b y T10c.
  - Cubre: REQ-003, REQ-005. ADR-011, ADR-015.
- [x] **1.6 MemoriaCalculo y auditoría** — *memoria por línea en el motor y reconstrucción probada; la escritura en `memoria_calculo` y `evento_auditoria` la hacen los casos de uso de los bloques 2 a 5*
  - Append-only por `LineaOfa`: reglas/versiones, versión de convenio, versión de snapshot, TC, ajustes; `EventoAuditoria` transversal.
  - Cubre: REQ-011, RNF-01. ADR-010. Verificación: reconstruir un cálculo desde la memoria en un test.

## Fase 2 — Capa configurable y OFA

- [ ] **2.1 Reglas configurables (nivel 2)**
  - `ReglaConfigurable` condición→acción, versionada; evaluada en el pipeline; CRUD con auditoría.
  - Cubre: REQ-006, RN-070. ADR-008.
- [ ] **2.2 Ajuste manual asistido (nivel 3) + promoción**
  - Ajuste de concepto/monto/excepción con usuario/fecha/motivo/valor ant.-nuevo; detección de recurrencia; promoción a regla configurable.
  - Es el paso de **revisión del facturador**: agrega lo que el sistema no sabe (reglas que solo conoce la persona). Motivo obligatorio. La IA nunca fija el monto.
  - **Corregir el tipo de retiro** (directo/indirecto) cuando está mal: `PATCH /operaciones/{id}` con motivo. Recalcula la OFA como versión nueva, conserva la anterior, mantiene los servicios agregados a mano y no recalcula una OFA ya enviada (409). Prueba: cambiar DIRECTO↔INDIRECTO en una operación sin convenio cambia los conceptos cobrados (T10c) y deja auditoría con valor anterior y nuevo.
  - **Botón "Enviar a SAP" visible desde la Fase A, pero desactivado** por la configuración `EnvioSapHabilitado=false`; `POST /ofa/{id}/enviar` responde 409 `ENVIO_SAP_DESHABILITADO`. Se activa en la Fase B (3.3) sin cambiar la pantalla.
  - **Agregar un servicio a mano** (`POST /ofa/{id}/lineas`): servicio del catálogo, cantidad, tarifa opcional (convenio o pública) y motivo. La línea queda marcada `AGREGADO_MANUAL` en su memoria de cálculo, con usuario, fecha y motivo. Si el mismo agregado se repite, se propone promoverlo a regla configurable. Prueba: línea agregada suma al total y la memoria reconstruye el monto.
  - Cubre: REQ-006, RN-070, BR-023. ADR-008.
- [ ] **2.3 Armado y validación de la OFA**
  - Cabecera + `LineaOfa`; validación de completitud (destinatario, ítems con tarifa o marcados, artículo SAP vinculado, recargos/días resueltos); bloqueo si incompleta.
  - En la Fase A la OFA validada queda **lista para digitar en SAP** (vista "Para digitar en SAP" con kit sugerido y lista de precios). El envío automático es de la Fase B (3.3).
  - Cubre: REQ-008, RN-059/060. ADR-010, ADR-017.
- [ ] **2.4 Detección de omisiones (cruce determinístico)**
  - Cruce prestado↔valorizado; alerta de faltantes (reefer, inspecciones, complementarias, forwarder); propone ítem, decisión registrada; necesidad de complementaria tardía (RN-056).
  - Cubre: REQ-007, RN-068, BR-003/013. ADR-009. (Propuesta por IA → tarea 4.3, opcional.)

## Fase 3 — Integraciones (puerto + stub primero, adaptador real al confirmar PA)

- [ ] **3.1a Ingesta mínima del evento facturable** (Fase A) — *nueva, ampliada el 2026-11-04*
  - **Contrato del mensaje:** `docs/arquitectura/contratos-integracion.md` §2 (campos con prioridad A, B y C y su origen) y esquema `EventoFacturable` del `openapi.yaml`. Es lo que AEXSA SYS enviará; construye contra ese formato.
  - Puerto `IEventoFacturablePort` con **adaptador simulado**. Entrada: `POST /aexsa/eventos` (push) y `POST /operaciones` (alta manual por el facturador, **puente** hasta que exista la ingesta real). Ambos usan el mismo esquema.
  - **Idempotencia y versiones:** la clave es `entidad` + `operacionIdAexsa` + `version` (contrato v3.0). En la etapa 1 solo se acepta `entidad = VISADO_VIRTUAL`, con un solo ID en `operacionIdAexsa`; `VISADO_PRESENCIAL` responde 400 `ENTIDAD_NO_HABILITADA`. Cada mensaje trae el estado completo. Una versión mayor **actualiza** la operación; una igual se registra como `DUPLICADO` (409 `YA_RECIBIDO`); una menor que la aplicada se registra `RECHAZADO` con `VERSION_SUPERADA` (409) y no cambia nada. Algoritmo transaccional con `SELECT … FOR UPDATE` en `modelo-datos.md` §4b; la base lo respalda con el trigger que impide que `version_evento` baje. Pruebas: versiones en desorden (v3 antes que v2), dos mensajes simultáneos de la misma operación nueva.
  - **Registro de cada mensaje** en `evento_aexsa` (append-only), con el resultado `APLICADO`, `DUPLICADO` o `RECHAZADO` y el detalle del error. Permite auditar y reprocesar.
  - **Persistencia:** `operacion`, `contenedor_operacion` y `servicio_prestado` (ver `modelo-datos.md` §3.3). Los clientes se crean o se buscan por RUT (consignatario y cliente facturador).
  - **Reglas al ingresar:** la validación DIN↔papeleta (RN-001) la hace el visador en AEXSA antes de aprobar, porque `papeletas` trae solo IDs `[POR CONFIRMAR]` (contratos §2 nota 14); clasificación de ruta {carga}×{retiro}×{convenio} (REQ-002, RN-003), **solo si el tipo de retiro viene informado** (si es `null` queda pendiente); momento de facturación (RN-046: con convenio, retener hasta la fecha de despacho, tope 7 días; mientras no exista fecha de despacho se usa `fechaRetiro` y se muestra como tal); destinatario por defecto: el consignatario, salvo que AEXSA indique otro (RN-077); una operación `BULTO` se guarda marcada como fuera de alcance (2ª etapa). Un mensaje con `estadoOperacion = ANULADA` marca la operación como anulada y, si ya tenía una OFA enviada o emitida, genera una alerta.
  - **Terminal:** AEXSA envía `id` y `nombre`, o `null` (cerca del 18 % de los documentos no tiene terminal). El Satélite resuelve el grupo con la tabla `terminal_mapa` (`STI`, `DP_WORLD`, `OTRO` o `SIN_MAPEAR`). Un terminal nuevo o ausente queda `SIN_MAPEAR`. Mientras esté así, la regla del directo sin convenio **no decide** la refacturación y la operación queda pendiente. Incluye la pantalla de mantenimiento (`/configuracion/terminales`, rol Administrador). Propuesta a confirmar: Puerto Central corresponde a DP World `[SUPUESTO]`.
  - **Convenio (revisado 2026-11-05): lo determina ZARPE, no AEXSA.** El mensaje no trae "tiene convenio". Al ingresar, ZARPE busca el convenio vigente por el RUT del cliente facturador (o del consignatario si no hay facturador) y la fecha de la operación (RN-043, RN-071). Si lo encuentra, guarda la versión usada; si no, la operación es sin convenio y se cobra con tarifa pública. Si hay más de un convenio vigente, aplica RN-018 (el más antiguo) y deja el pendiente `VARIOS_CONVENIOS_VIGENTES` para que el facturador confirme. La pantalla muestra siempre "Convenio encontrado: …" o "Sin convenio para el RUT …", para que el facturador detecte un convenio no cargado. Si después se carga o cambia un convenio, las operaciones aún no validadas se recalculan.
  - **Documento con varias solicitudes:** el mensaje trae los documentos en `bls` (`id` y `numero`). Si ya existe otra operación activa que comparte un `bls[].id`, ambas quedan con el pendiente `DOCUMENTO_CON_VARIAS_SOLICITUDES` y la validación de la OFA se bloquea hasta que el facturador resuelva cuál vale (evita facturar dos veces).
  - **Campos que pueden venir vacíos:** tipo de retiro, sentido, cantidad de DUS, fecha de despacho, días y término del aforo, cantidad de un servicio, y los de prioridad C. El Satélite no falla: calcula la lista `pendientes` de la operación, la página del facturador muestra qué falta y permite completarlo con `PATCH /operaciones/{id}` (motivo obligatorio, auditable). Un dato completado a mano no se pisa con un mensaje posterior salvo que AEXSA traiga un valor no vacío; en ese caso se registra el conflicto y se pide decidir.
  - **Unidad de los servicios:** AEXSA la envía como texto libre. El Satélite no la usa para el cobro: toma la unidad normalizada del catálogo por `tipoServicioId` (tarea 1.1).
  - **Inspecciones (contrato v3.0, contratos §2 nota 15):** el `tipo` llega como código del catálogo de AEXSA y se resuelve con la tabla `tipo_inspeccion_mapa` (`AFORO_ADUANA`, `SAG`, `OTRA` o `SIN_MAPEAR`), con pantalla de mantenimiento `/configuracion/tipos-inspeccion` (rol Administrador). Los días bajo aforo salen de la inspección `AFORO_ADUANA`; si está sin mapear o sin término, pendiente `DIAS_AFORO`. Una inspección sin `finalizadaEn` o sin resultado deja `INSPECCION_NO_FINALIZADA` (RN-058); `RECHAZADA_PARCIAL` o `RECHAZADA_TOTAL` deja `INSPECCION_RECHAZADA`, que el facturador resuelve con motivo. El resultado no cambia el precio.
  - **La validación de la OFA (2.3) bloquea** una operación con pendientes que afecten el monto (`TIPO_RETIRO`, `TERMINAL_SIN_MAPEAR` en directo sin convenio, `CANTIDAD_SERVICIO`, `CANTIDAD_DUS` en exportación, `DOCUMENTO_CON_VARIAS_SOLICITUDES`, `INSPECCION_NO_FINALIZADA`). `VARIOS_CONVENIOS_VIGENTES` e `INSPECCION_RECHAZADA` exigen una decisión registrada antes de validar.
  - **Pruebas adicionales:** terminal `null`; cliente con convenio vigente (lo encuentra por RUT); cliente sin convenio (tarifa pública); convenio vencido a la fecha de la operación; dos convenios vigentes; convenio cargado después de llegar la operación (recalcula); dos solicitudes del mismo documento.
  - **Pruebas:** mensaje válido; mensaje repetido; versión nueva que actualiza; versión vieja que se rechaza; `VISADO_PRESENCIAL` o visado virtual con más de un ID rechazados; dos operaciones que comparten un `bls[].id`; días bajo aforo desde la inspección de aforo; directo en STI y en DP World; operación de carga suelta; mensaje con campos B vacíos. Usa datos inventados.
  - `[SUPUESTO]` El puente manual cubre la Fase A mientras AEXSA SYS no entregue el evento. Confirmar con negocio y con TI.
  - Cubre: REQ-001, REQ-002, REQ-003. ADR-005.
- [ ] **3.1b Conexión real con AEXSA SYS: recepción y conciliación** (Fase B; en cuanto AEXSA entregue la etapa 1)
  - **Esquema acordado tras la revisión del código de AEXSA:** AEXSA registra el mensaje en una tabla de salida en la misma transacción que la aprobación del visado y un proceso lo envía con `POST /aexsa/eventos`, con reintentos. El Satélite responde según la tabla de `contratos-integracion.md` §2: 200 o 201 (aplicado), 409 (`YA_RECIBIDO` o `VERSION_SUPERADA`, sin reintento) o 400 (rechazado, sin reintento).
  - **Conciliación:** adaptador de consulta contra un `GET` de solo lectura que AEXSA expone sobre su tabla de salida. Cuadratura eventos AEXSA ↔ operaciones del Satélite; lo que falte se recupera por esta vía.
  - Autenticación de la llamada de AEXSA hacia el Satélite y si pasa por Agunsa Connect: `[POR CONFIRMAR]` (PA-002). Mientras tanto la seguridad del endpoint es configurable.
  - Entrega de AEXSA en dos etapas: etapa 1 con `VISADO_APROBADO`, anulación y el `GET`; etapa 2 con `OPERACION_ACTUALIZADA` completo.
  - Cubre: REQ-001. ADR-005. **(bloqueada por PA-006: falta la fecha de AEXSA.)**
- [ ] **3.2 Lectura de tarifas desde SAP por API + snapshot** (Fase B; reemplaza la importación por archivo de 1.1b)
  - Puerto `ISapCatalogoPort` (el adaptador de archivo ya existe desde 1.1b); sync programada → `catalogo_snapshot` versionado; mapeo RUT→CardCode, condición de pago/lista de precios (RN-065).
  - Cubre: REQ-005 (fuente), RN-014/061. ADR-003, ADR-017. **(real bloqueada por PA-003/PA-007; stub con datos de ejemplo.)**
- [ ] **3.3 Outbox + despachador de la OFA hacia SAP**
  - `OutboxMessage` en la misma transacción que la OFA; worker con reintentos/backoff; idempotencia por `OfaId`+versión; `EMITIDA_SIN_CONFIRMACION` + alerta; log request/response/error; reproceso desde UI.
  - Cubre: REQ-008, REQ-009, RNF-07. ADR-002. **(adaptador real bloqueado por PA-001/PA-002; se prueba con `AgunsaConnectSapAdapter` stub.)**
- [ ] **3.4 Recepción de folio/DTE y conciliación**
  - Endpoint callback de folio; recibe DTE XML; concilia calculado↔DTE por folio; cierra vínculo operación↔factura.
  - Cubre: REQ-009, RN-029/076. ADR-002, ADR-004. **(bloqueada por PA-002.)**
- [ ] **3.5 Retorno a AEXSA SYS: ZARPE informa la factura emitida** — *ratificada el 2026-11-05: Agunsa Connect avisa a ZARPE que se facturó (3.4) y ZARPE informa a AEXSA, para tener la trazabilidad completa*
  - Puerto `IAexsaRetornoPort` con adaptador simulado. Al conciliar el folio (3.4), en la misma transacción se escribe un `outbox_message` de tipo `RETORNO_AEXSA`. El Worker lo envía al endpoint de recepción de AEXSA con reintentos; registra solicitud, respuesta y error; alerta al agotar reintentos; reenvío desde la pantalla con `POST /ofa/{id}/reenviar-aexsa`. Idempotente por folio.
  - Contenido: `entidad`, `operacionIdAexsa`, `bls`, folio, tipo de DTE, montos neto y total, fecha de emisión y XML del DTE (ver `contratos-integracion.md` §6). Con el acuse de AEXSA, la OFA pasa a `INFORMADA_AEXSA`.
  - Pruebas: acuse recibido; AEXSA caído y reintento; folio repetido (no duplica); reenvío manual; reintentos agotados con alerta.
  - Dependencia: el equipo de AEXSA construye el endpoint de recepción `[POR CONFIRMAR]` (PA-004).
  - `[POR CONFIRMAR]` En AEXSA las operaciones con convenio no emiten factura dentro del sistema (recorrido `PREAPROBADO_CONVENIO` → `PRDIN_EMITIDA` → `CONFORMADA`). El retorno del folio probablemente solo aplica a las operaciones sin convenio. Confirmar con TI de AEXSA y con facturación antes de construir.
  - Las operaciones con convenio se facturan en consolidado, fuera de AEXSA (RN-055): la consolidación de varias operaciones en una factura está abierta como PA-010 y condiciona la Fase B.
  - Cubre: REQ-009, RN-072/074. ADR-004. **(bloqueada por PA-004/PA-008.)**
- [ ] **3.6 Conciliación de pagos**
  - Recibe estado de pago por folio desde SAP; soporta pago agrupado multi-factura/multi-cliente (RN-067); alerta de impago; saldo a favor/devolución (registro).
  - Cubre: REQ-010, RN-063/067/069. ADR-004, ADR-010.

## Fase 4 — Capacidades asistidas por IA — **ETAPA OPCIONAL**

> No se construye salvo que el negocio lo pida. El sistema funciona completo sin esta fase. Toda salida de IA es una **propuesta** con confirmación humana; la IA nunca fija un monto.

- [ ] **4.1 Puerto de IA y gobierno de confianza** *(opcional)*
  - `IExtraccionConvenio` / `IDeteccionOmisiones`; umbral de confianza; toda salida de IA es propuesta con confirmación humana.
  - Cubre: principios, RN-070. ADR-007/008/009.
- [ ] **4.2 Extracción asistida de convenios** *(opcional)*
  - Lee PDF/Word/correo → `PropuestaConvenio` en estado BORRADOR; confirmación humana → VIGENTE; documento origen en S3. Reutiliza el mismo flujo de confirmación del formulario manual (1.2).
  - Cubre: REQ-012, RN-071. ADR-007, ADR-012. Carga inicial en lote de los convenios existentes (ADR-012 §7).
- [ ] **4.3 Detección de omisiones asistida por IA** *(opcional)*
  - Propone faltantes por patrones/anomalías sobre datos de facturación/operación (no de pago, RN-068); confirmación humana.
  - Cubre: REQ-007. ADR-009.

## Fase 5 — Frontend, trazabilidad y cierre

- [ ] **5.1a Frontend Angular de la Fase A** — *dividida*
  - Referencia visual: `docs/arquitectura/mapa-pantallas.md` y el prototipo navegable (se entrega por canal interno).
  - Pantallas: login y roles; convenios (C1 a C4, formulario manual); catálogo SAP (T3); operaciones (F1, F2); OFA con cálculo, ajuste y "Para digitar en SAP" (F3, F4, F6 en su forma de la Fase A); omisiones (F5); reglas configurables.
  - Sin tarjetas de indicadores (KPI) en la primera versión: lo simple primero.
  - Cubre: REQ-004/006/008/012. ADR-001, ADR-016.
- [ ] **5.1b Frontend de recaudación y cuadraturas** (Fase B)
  - Pantallas: facturas y folio (R1), pagos y conciliación (R2), impagos (R3), trazabilidad (T1), cuadraturas y alertas (T2), envío a SAP (F6 completo, reproceso del Outbox).
  - Cubre: REQ-009/010. ADR-001.
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

## Definición de "hecho" (para cada tarea)

Una tarea se marca `[x]` solo si cumple **todo** esto:
1. Compila y el pipeline de CI está en verde.
2. Tiene pruebas automáticas y pasan. En el motor de tarifas, los casos T1 a T12 son obligatorios.
3. Los endpoints coinciden con `docs/arquitectura/api/openapi.yaml`. Si el contrato cambia, se actualiza el archivo en el mismo cambio.
4. No hay secretos ni datos reales de clientes en el repositorio.
5. Todo ajuste o decisión que cambia un monto queda registrado en la auditoría.
6. Se completó el registro de avance de la tarea (tabla de abajo).

## Hitos y dependencias externas

| Entrega | Contenido | Valor para negocio | Depende de |
|---------|-----------|--------------------|-----------|
| **E1 — Convenios** | Fundaciones + autenticación + convenios versionados (formulario manual) | Fuente única del convenio vigente; fin de la búsqueda en correos | nada externo (app client de Cognito: PA-009) |
| **E2 — Factura con valores ajustados** | Catálogo + motor determinístico + capa configurable + OFA + ingesta mínima (prefactura para digitar) | Menos errores y notas de crédito; cálculo automático | archivos de catálogo de SAP |
| **E3 — Detección de omisiones** | Cruce prestado↔valorizado | Recupera cobros que hoy se pierden | nada externo |
| **E4 — Integración automática con SAP** | Lectura real + Outbox + escritura OFA + folio | Elimina la digitación manual en SAP | **PA-001, PA-002, PA-003** |
| **E5 — Cierre del lazo y pagos** | Ingesta real + retorno a AEXSA + conciliación de pagos + frontend de recaudación + seguridad | Check de salida automático; trazabilidad op→factura→pago | **PA-004, PA-005, PA-006, PA-008** |

> **Fase A = E1 + E2 + E3:** valor operativo temprano. No depende de terceros salvo la exportación de catálogo de SAP y el app client de Cognito.
>
> **Fase B = E4 + E5:** elimina la digitación y cierra el círculo. Depende de terceros (proveedor SAP, Agunsa Connect, AEXSA SYS, ciberseguridad) y su plazo se estima y gestiona aparte.

## Puntos abiertos que no frenan el inicio

| Tema | Qué hacer mientras tanto |
|---|---|
| Cómo llega el evento en la Fase A (PA-006) | Construir 3.1a con simulador y alta manual. Confirmar con negocio y TI |
| Si el folio vuelve al Satélite en la Fase A | `[POR CONFIRMAR]`. En la Fase A el facturador digita en SAP; la OFA queda "lista para digitar". Si se quiere registrar el folio a mano, se define con negocio |
| App client y grupos de roles de Cognito (PA-009) | Autoridad y audiencia configurables; manejador de autenticación de prueba |
| Cómo se arma un kit en SAP y fechas de los archivos (PQ-025) | Archivos de ejemplo con el formato documentado |
| Escritura en SAP y contrato de Agunsa Connect (PA-001, PA-002) | Simulador `AgunsaConnectSapAdapter`; el contrato está en `contratos-integracion.md` |
| Fecha de inicio y plazo | Los define la jefatura de TI. Esta lista de tareas no depende de eso |
| **Precarga de los convenios vigentes** `[PENDIENTE]` (decisión 2026-11-05) | Se hará a mano con el formulario de convenios (tarea 1.2), sin importación masiva. Quién la hace, cuántos convenios son y la fecha quedan por definir. **Debe estar lista antes de probar la Fase A con negocio**: un convenio no cargado se cobra con tarifa pública. No bloquea el desarrollo; las pruebas usan convenios inventados |

## Registro de avance

Completar una fila por tarea terminada (regla del procedimiento de desarrollo: archivos generados, pruebas, hallazgos y pendientes).

| Tarea | Fecha | Archivos generados | Pruebas (cantidad y resultado) | Hallazgos | Pendientes abiertos |
|---|---|---|---|---|---|
| 0.1 (parcial) + 0.4 (parcial) + base de 5.1a | 2026-11-13 | `Fuentes/AEXSA.ZARPE.BACKEND.NET` (solución con `Satelite.Dominio`, `.Aplicacion`, `.Infraestructura`, `.Api`, `.Tests`) y `Fuentes/AEXSA.ZARPE.FRONTEND.ANGULAR` (Angular 22.2), desde el estándar AGUNSA BPT | Backend: 2 pruebas de arquitectura, verdes. Frontend: build de producción sin advertencias | El estándar BPT trae login propio con contraseñas; se reemplazó por Cognito (ADR-016). Su `appsettings.json` trae en claro una contraseña SMTP y la clave JWT: no se copiaron | CI (0.3); valores de Cognito y nombres de grupos (PA-009); matriz rol→acceso `[SUPUESTO]`; migración inicial (0.2); `Satelite.Worker` en etapa 2 |
| 0.2 + 1.3 + 1.4 + 1.5 (motor) + 1.6 (memoria en el motor) — etapa 1, bloque 1 | 2026-11-13 | Backend: `Satelite.Dominio/Comun` (enums de los CHECK, `Rut`), `Satelite.Dominio/Entidades` (35 tablas de 01 + 02), `Satelite.Dominio/Motor` (`ContextoCalculo`, `ResultadoCalculo`, `MotorTarifas`, `DiasBajoAforo`, `Reglas/R0` a `R7`), `Satelite.Infraestructura/Persistencia` (`Configuraciones/`, columnas en snake_case, `DisenoDbContextFactory`, migración `Inicial` que ejecuta copias de `db/init/01` y `02` embebidas en `Migraciones/Sql/`), `Satelite.Tests/Motor` y `Satelite.Tests/Infraestructura` (`PostgresFixture` con Testcontainers 4.16.0). Base: `db/recrear-con-migracion.ps1`, `db/semillas/` | `dotnet test`: 23 de 23 verdes (2 de arquitectura; T1–T12 con T10b, T10c, directo con convenio y conteo de días de aforo: 16; reconstrucción desde la memoria: 2; migración aplicada, sin cambios pendientes del modelo y trigger de versión: 3). `dotnet ef migrations has-pending-model-changes`: sin cambios. `recrear-con-migracion.ps1`: 0 diferencias de esquema con `pg_dump -s` y pruebas SQL ok = 63, fallas = 0 | Sin cambios de esquema `[E1-n]`: la migración reproduce 01 + 02 tal cual. EF Core agrega al snapshot índices de FK que la base no tiene (no afecta: el esquema real lo crea el SQL). Montos sin redondeo (4 decimales en la base): no hay regla de redondeo documentada. Supuestos marcados en el código: días bajo aforo por fecha calendario UTC (`DiasBajoAforo`, nota 15 `[POR CONFIRMAR]`); el piso `tc_minimo` solo aplica a tarifas del convenio, no a la pública de excepción; terminal `OTRO` cobra la refacturación del directo con tarifa pública como STI; recargos sobre el monto antes de recargos, sin componerse | Regla de redondeo de montos; confirmar los 3 supuestos del motor con facturación; repartido manual (1.5); escritura de memoria y auditoría en los casos de uso (bloques 4 y 5) |
