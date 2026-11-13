# Contratos de integración — ZARPE - FACTURACIÓN

Versión: 1.0
Fecha: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Base: ADR-001 (puertos y adaptadores), ADR-002 (Outbox + Agunsa Connect), ADR-003/004/005, `design.md` §6.
Propósito: definir **cómo se consumen** las integraciones — el contrato de cada puerto campo a campo, el patrón de consumo, la idempotencia y el manejo de error. Es entregable de construcción.

> Decisión de consumo (ADR-001/002): el Satélite **no llama directo** a SAP ni a AEXSA. Cada integración se expone como un **puerto** (interfaz del dominio) implementado por un **adaptador**; la comunicación real pasa por **Agunsa Connect**. La **escritura hacia SAP es asíncrona vía Outbox** (nunca síncrona ni fire-and-forget). Hasta resolver PA-001/PA-002, los adaptadores reales se sustituyen por **stubs** sin cambiar el dominio.

---

## 1. Mapa de puertos y patrón de consumo

| Puerto | Dirección | Patrón de consumo | Idempotencia | Estado |
|--------|-----------|-------------------|--------------|--------|
| `IEventoFacturablePort` | AEXSA → Satélite | **Push**: AEXSA envía `POST /aexsa/eventos` desde su tabla de salida, con reintentos. Un `GET` de solo lectura en AEXSA sirve para conciliar y recuperar lo que falte (ADR-005) | `operacion_id_aexsa` + `version` | PA-006 |
| `ISapCatalogoPort` | SAP → Satélite | Consulta (request/response) + snapshot versionado local | versión de snapshot | PA-003/PA-007 |
| `ISapOrdenVentaPort` | Satélite → SAP | **Outbox asíncrono** → Agunsa Connect | `clave_idempotencia` (OfaId+versión) | PA-001/PA-002 |
| (callback de folio) | SAP → Satélite | Callback HTTP a endpoint del Satélite | `folio` | PA-002 |
| `IAexsaRetornoPort` | Satélite → AEXSA | **Outbox** → endpoint de recepción nuevo en AEXSA, con acuse | `folio` | PA-004/PA-008 |
| `IExtraccionConvenioPort` | IA → Satélite | Request/response; salida es **propuesta** con confirmación humana | — | — |

Todos los contratos: transporte y seguridad los fija Agunsa Connect (PA-002); aquí se define el **contrato de datos**, independiente del transporte. Todo intercambio registra request/response/error para auditoría y reproceso (ADR-002).

---

## 2. `IEventoFacturablePort` — entrada del evento (REQ-001, ADR-005)

**Cómo se consume:** AEXSA **envía** el evento (push) al `POST /aexsa/eventos` del Satélite. Lo registra en una tabla de salida en la misma transacción que la aprobación del visado y un proceso lo envía con reintentos. El Satélite no consulta a AEXSA para recibir eventos. Un `GET` de solo lectura sobre esa tabla de salida queda solo para **conciliar** y recuperar lo que no llegó. Cada operación se procesa **una sola vez** (idempotencia por `operacionIdAexsa` + `version`). Ver ADR-005.

**Cuándo se envía:** AEXSA envía al aprobarse el visado, y el Satélite decide cuándo corresponde facturar (RN-046: con convenio, se retiene hasta la fecha de despacho, con tope de 7 días). Si el negocio quiere que AEXSA envíe en otro momento, se cambia el disparador, no el mensaje `[POR CONFIRMAR]`.

```csharp
// Entrada (push): caso de uso que invoca POST /aexsa/eventos. Aplica el algoritmo de modelo-datos.md §4b.
public interface IIngestaEventoFacturable
{
    Task<ResultadoIngesta> IngresarAsync(EventoFacturableDto evento, CancellationToken ct);
}

// Conciliación: adaptador del GET de solo lectura que AEXSA expone sobre su tabla de salida (no es la vía de entrada).
public interface IEventoFacturablePort
{
    Task<IReadOnlyList<EventoFacturableDto>> ObtenerDesdeAsync(DateTimeOffset desde, CancellationToken ct);
}
```

**Respuesta de `POST /aexsa/eventos`** (igual en el OpenAPI):

| Código | `resultado` | Cuándo | Qué hace AEXSA |
|---|---|---|---|
| `201` | `APLICADO` | Operación nueva | Lo da por enviado |
| `200` | `APLICADO` | Versión mayor que la aplicada: actualiza | Lo da por enviado |
| `409` | `DUPLICADO` (`YA_RECIBIDO`) | Misma versión ya aplicada | Lo da por enviado, sin reintento |
| `409` | `RECHAZADO` (`VERSION_SUPERADA`) | Versión menor que la aplicada (llegó tarde) | Lo da por enviado, sin reintento |
| `400` | `RECHAZADO` | Mensaje inválido | Sin reintento; avisa para corregir |
| `401` | — | Credencial inválida | Sin reintento; avisa |
| `5xx` o sin respuesta | — | Falla del Satélite | Reintenta con espera creciente |

**Cada mensaje trae el estado completo** de la operación, no solo lo que cambió. Si se pierde una versión intermedia, la siguiente basta.

```jsonc
// EventoFacturableDto  (v2.2: sin conConvenio). Prioridad: A = el campo siempre viaja (puede ir null donde se indica), B = necesario en casos concretos, C = deseable.
// "Puede venir vacío" = el Satélite NO falla: lo marca como pendiente y el facturador lo completa a mano, con registro.
{
  // --- Control del mensaje
  "tipoEvento": "VISADO_APROBADO",        // A · VISADO_APROBADO | OPERACION_ACTUALIZADA
  "version": 1,                           // A · sube con cada actualización; el Satélite actualiza, no duplica
  "operacionIdAexsa": "98933",            // A · clave de idempotencia = SolicitudVisadoID, enviado como texto
  "documentoCargaId": "184520",           // A · documento de carga al que pertenece. El 1,9 % de los documentos tiene más de una solicitud de visado: sin este dato, el Satélite podría facturar dos veces el mismo documento
  "visadoAprobadoEn": "2026-10-26T14:30:00Z", // A · AEXSA lo guarda con zona horaria; se envía en UTC
  "estadoOperacion": "ACTIVA",            // A · ACTIVA | ANULADA (visado devuelto, anulado o rectificado después de enviado)

  // --- Clasificación de la ruta (REQ-002)
  "tipoCarga": "CONTENEDOR",              // A · CONTENEDOR | BULTO (RN-027). BULTO es 2ª etapa: se recibe y se marca fuera de alcance
  "sentido": "IMPORTACION",               // A · IMPORTACION | EXPORTACION (impo usa DIN, expo usa DUS)
  "tipoRetiro": "DIRECTO",                // A · DIRECTO | INDIRECTO | null (RN-026). PUEDE VENIR VACÍO: en AEXSA está vacío en el 95,6 % de los contenedores del ambiente revisado. NO se deriva de IndicaRetiroDirecto (no coincide con el directo que se factura): si falta, se envía null y lo define el facturador
  // (revisado 2026-11-05) Ya NO viaja "conConvenio": el convenio lo busca el Satélite por RUT y fecha. Es parte del valor del cálculo (nota 3)
  "terminal": { "id": 2, "nombre": "TERMINAL DE EJEMPLO" }, // A · catálogo MasterData.TerminalRetiro: hay 7, pero solo 2 tienen documentos, y el 18 % de los documentos no tiene terminal. PUEDE VENIR null. El Satélite lo asocia a STI / DP World / OTRO con una tabla de mapeo configurable (RN-079)

  // --- Documentos
  "din": "2026-6-123456",                 // A en impo
  "dus": [],                              // A en expo (lista)
  "cantidadDus": null,                    // B · captura manual en AEXSA (RN-047); si no existe, la completa el facturador
  "bls": ["BL-0001"],                     // B · una factura puede agrupar varios BL (RN-055)
  "papeleta": { "numero": "P-123", "rut": "76.123.456-7" }, // B · para contrastar con la DIN (RN-001)

  // --- Partes
  "consignatario": { "rut": "76.123.456-7", "razonSocial": "Cliente de ejemplo" }, // A · RUT de la DIN (RN-057)
  "clienteFacturador": {                  // B · null si nadie lo asignó; el Satélite usa el consignatario por defecto (RN-077)
    "rut": "77.888.999-0", "razonSocial": "Forwarder de ejemplo",
    "tipo": "EMISOR_BL",                  // EMISOR_BL | CLIENTE | GENERADOR_NEGOCIO | CONSIGNATARIO
    "asignadoPor": "usuario", "asignadoEn": "2026-10-26T14:10:00Z"
  },

  // --- Contenedores (almacenaje, despacho, aforo)
  "contenedores": [                       // B · obligatorio si hay almacenaje, aforo o facturación post-despacho
    {
      "numero": "ABCD1234567",
      "fechaRecepcion": "2026-10-20T08:00:00Z",
      "fechaRetiro": null,
      "fechaDespacho": null,              // B · con convenio se factura tras el despacho, tope 7 días (RN-046)
      "diasAlmacenaje": 12,               // B · el que AEXSA ya calcula en SQL (RN-033), sin descontar días libres
      "bajoAforo": { "asignadoEn": "2026-10-21T09:00:00Z", "finalizadoEn": null, "dias": null }, // B · RN-052 / RN-058. AEXSA solo guarda cuándo se asignó el aforo; el término y los días pueden venir vacíos
      "esReefer": false,                  // C
      "inspecciones": [ { "tipo": "SAG", "asignadaEn": "2026-10-21T09:00:00Z", "finalizadaEn": null } ] // B · RN-054 / RN-058. El término puede venir vacío
    }
  ],

  // --- Servicios prestados (base del cálculo y de la detección de omisiones, REQ-007)
  "servicios": [                          // A
    {
      "tipoServicioId": 42,               // A · MasterData.TipoServicio (RN-032)
      "cantidad": 2,                      // A · la columna admite vacío pero ningún servicio del ambiente revisado la tiene vacía. Si llega null, el Satélite no valora ese servicio hasta que el facturador la complete. No hay cantidad por defecto
      "unidad": "Contenedor",             // C · texto tal como está en AEXSA (sin normalizar). El Satélite normaliza por tipoServicioId (tarea 1.1)
      "contenedor": null,                 // C · AEXSA no lo guarda por servicio; llega vacío
      "finalizadoEn": "2026-10-24T10:00:00Z", // B · 89 % de los servicios tiene fecha de término
      "origen": "AEXSA"                   // C · AEXSA | LIQUIDACION_PUERTO. AEXSA lo deriva por el código del tipo de servicio (SRV PUERTO, SECURITY FEE PORTUARIO): ver nota 6
    }
  ]
}
```

**No debe viajar:** los montos tecleados a mano en AEXSA (`MontoFacturaCosto`, `MontoFacturaVenta`), porque el cálculo es del Satélite (RN-025); credenciales; ni datos personales que no estén en la lista.

**Cuándo se envía:** al aprobarse el visado (`VISADO_APROBADO`) y cada vez que cambie algo que afecte el cobro antes de facturar (`OPERACION_ACTUALIZADA`): fecha de despacho o retiro, servicios nuevos, reclasificación directo→indirecto (RN-044), cliente facturador asignado. Si el visado se devuelve o se anula después de enviado, se envía una actualización con `estadoOperacion = ANULADA`.

**Disparador en pantalla:** el botón **"Aprueba"** de "Respuesta pre-visado" (Visado Virtual → Panel Visador → acciones de la solicitud). El visador lo presiona después de revisar que los documentos están correctos. El visador **no** define si hay convenio: eso lo resuelve el Satélite (nota 3).

### Notas de la revisión del código de AEXSA SYS (2026-11)

Fuente: documento de diseño `AEXSA-SAT-API` (revisión de código y de la base de desarrollo, con datos hasta 2026-06-22). Es un ambiente de desarrollo: las cifras de completitud hay que validarlas en producción.

1. **Lo que AEXSA no tiene hoy:** cantidad de DUS (y una lista de DUS propia), fecha de despacho por contenedor, fecha de término del aforo, contenedor por servicio y origen "liquidación de puerto". El mensaje los admite vacíos; el Satélite los marca como **pendientes** y el facturador los completa.
2. **Terminal.** Hay 7 terminales de retiro, pero solo 2 tienen documentos (San Antonio Terminal Internacional y Puerto Central) y el 18 % de los documentos no tiene terminal. AEXSA envía el terminal tal cual (`id` y `nombre`) o `null`. El Satélite mantiene una tabla de mapeo `terminal_mapa` (id de AEXSA → `STI`, `DP_WORLD` u `OTRO`), editable por el rol Administrador. Un terminal sin mapear o ausente queda `SIN_MAPEAR`: el cálculo del directo sin convenio **no decide** la refacturación y deja la operación pendiente. Propuesta a confirmar por la jefatura de facturación: Puerto Central corresponde a DP World. `[SUPUESTO]` (se deduce por descarte; no hay evidencia directa)
3. **Convenio (revisado 2026-11-05, decisión del negocio).** AEXSA envía `VISADO_APROBADO` para **todas** las operaciones aprobadas, sin indicar si tienen convenio. **El Satélite busca el convenio** vigente por el RUT del cliente facturador (o del consignatario) y la fecha de la operación, en su propio registro de convenios (fuente autoritativa, RN-071). Determinar el convenio y calcular con él es el valor central del Satélite; no depende de lo que responda una persona en AEXSA. Si no encuentra convenio, cobra con tarifa pública; si encuentra más de uno vigente, aplica RN-018 y deja el pendiente `VARIOS_CONVENIOS_VIGENTES`. Con convenio, el Satélite retiene la operación hasta el despacho (RN-046). `[POR CONFIRMAR]` con el equipo de AEXSA: el cuadro "¿el cliente tiene convenio?" y el estado `PREAPROBADO_CONVENIO` de AEXSA hoy cambian el recorrido de la operación dentro de AEXSA; definir si se quitan o se mantienen solo para el flujo interno de AEXSA. En cualquier caso, el Satélite no los usa para cobrar.
4. **Fecha de despacho.** No existe como campo en AEXSA (candidatos: salida del contenedor o fecha de retiro). Cada solicitud puede traer adjunta una guía de despacho, pero no se sabe si sirve como fuente. Hasta que se defina qué equivale a "despacho", el Satélite usa `fechaRetiro` como referencia y lo muestra como tal. `[SUPUESTO]`
5. **Tipo de retiro.** Puede venir vacío y **no se deriva** de `IndicaRetiroDirecto`: la revisión mostró que no coincide con el tipo de retiro de facturación (por ejemplo, 2.316 de 3.457 contenedores marcados DIRECTO tienen `IndicaRetiroDirecto` en falso). Si llega `null`, el Satélite no clasifica la ruta: queda pendiente y el facturador lo elige.
6. **Cargos de puerto (RN-056).** No hay una tabla de liquidación. Existen como tipos de servicio normales, con código `SRV PUERTO` (almacenaje, conexión, manipuleo, otros y pronto despacho) y `SECURITY FEE PORTUARIO`; no se encontró trinca. AEXSA los marca con `origen = LIQUIDACION_PUERTO` por ese código. Falta confirmar si se cargan a mano o se importan. Para detectar un cargo que llega **después** de facturar hace falta la etapa 2 (`OPERACION_ACTUALIZADA` por servicios nuevos): con solo la etapa 1, ese aviso no funciona. `[POR CONFIRMAR]`
7. **Cantidad de un servicio.** Ningún servicio del ambiente revisado la tiene vacía, así que no hay cantidad por defecto. Si llega `null`, el servicio no se valoriza hasta que el facturador la complete.
8. **Unidad.** En AEXSA es texto sin normalizar (por ejemplo "Contenedor", "Contenedor / Día", "Por B/L", "Vehículo"). Se envía como viene, solo como información. El Satélite normaliza a su enumeración por `tipoServicioId`, con el catálogo de la tarea 1.1.
9. **Cliente facturador.** Casi siempre vacío (66 de 191.906 con valor). Es el caso normal: el Satélite usa al consignatario y el facturador reparte a mano si hace falta (RN-050).
10. **Cómo se envía.** AEXSA registra el mensaje en una tabla de salida **en la misma transacción** que la aprobación del visado, y un proceso lo envía con `POST /aexsa/eventos` con reintentos. Idempotencia por `operacionIdAexsa` + `version`; los códigos de respuesta están en la tabla de arriba (`409` cubre tanto el repetido como la versión superada). AEXSA además expone un `GET` de solo lectura sobre la misma tabla, que el Satélite usa para **conciliar** (cuadratura eventos AEXSA ↔ operaciones del Satélite). Pendiente: autenticación de la llamada y si pasa por Agunsa Connect (PA-002).
11. **Entrega en dos etapas en AEXSA.** Etapa 1: `VISADO_APROBADO`, anulación y el `GET`. Etapa 2: `OPERACION_ACTUALIZADA` completo, porque los puntos donde cambian los datos están dispersos en AEXSA.

**Validaciones al consumir:** contrastar DIN↔papeleta; si discrepan → `facturable = false` y devolver a corrección (RN-001). Clasificar momento de facturación por convenio (RN-046).

---

## 3. `ISapCatalogoPort` — lectura de tarifas (REQ-005, ADR-003)

**Cómo se consume:** por **API de solo consulta** (no modifica SAP). El Satélite sincroniza de forma programada y **materializa un snapshot versionado**; el cálculo usa el snapshot, no la llamada en vivo (reproducibilidad).

```csharp
public interface ISapCatalogoPort
{
    Task<IReadOnlyList<ArticuloDto>>    ObtenerArticulosAsync(CancellationToken ct);
    Task<IReadOnlyList<PrecioDto>>      ObtenerListaPreciosAsync(string listaId, CancellationToken ct);
    Task<SocioNegocioDto?>              ObtenerSocioNegocioPorRutAsync(string rut, CancellationToken ct);
}
```

```jsonc
// PrecioDto
{ "articuloId": "SRV-REC-001", "listaPreciosId": "LP-CONV-STD",
  "precio": 15000.0000, "moneda": "CLP", "vigente": true }

// SocioNegocioDto (mapeo RUT -> SAP, PA-007)
{ "rut": "76.123.456-7", "cardCodeSap": "C76123456",
  "condicionPago": "CREDITO", "diasCredito": 30, "listaPreciosId": "LP-CONV-STD" }
```

---

## 4. `ISapOrdenVentaPort` — escritura de la OFA (REQ-008/009, ADR-002) — **el crítico**

**Cómo se consume (la opción levantada):** NO se llama en línea. Al validar la OFA, en la **misma transacción** se escribe un `outbox_message`. Un **worker** lee el Outbox y llama al adaptador de Agunsa Connect con **reintentos y backoff**. Si se agota, la OFA queda `EMITIDA_SIN_CONFIRMACION` y se alerta (RNF-07). Nunca se crea una segunda orden por la **clave de idempotencia**.

```csharp
public interface ISapOrdenVentaPort
{
    Task<ResultadoEmisionDto> CrearOrdenVentaAsync(OfaDto ofa, CancellationToken ct);
}
```

```jsonc
// OfaDto (lo que el Satelite entrega a SAP vía Agunsa Connect)
{
  "ofaId": "a1b2c3d4-...",
  "claveIdempotencia": "a1b2c3d4-v1",     // OfaId + version: evita doble orden
  "operacionIdAexsa": "OP-2026-000123",
  "asignaciones": [                        // repartido: 1..n destinatarios (RN-050/077)
    {
      "destinatarioRut": "76.123.456-7",
      "rol": "CONSIGNATARIO",
      "lineas": [
        { "tipoServicioId": 42, "articuloSap": "SRV-REC-001",
          "cantidad": 2, "unidad": "CTR",
          "tarifa": 15000.0000, "moneda": "CLP", "monto": 30000.0000 }
      ]
    }
  ]
}

// ResultadoEmisionDto (lo que vuelve)
{ "ofaId": "a1b2c3d4-...", "estado": "EMITIDA",   // EMITIDA | RECHAZADA | PENDIENTE
  "folio": "F-00098765", "numeroInterno": "OV-2026-5543",
  "mensajeError": null }
```

**Nota (ADR-017):** cada línea lleva `articuloSap`, el código del artículo en SAP, tomado del vínculo servicio↔artículo confirmado. Es obligatorio para crear la orden de venta. Una OFA con una línea sin artículo SAP no pasa la validación de completitud y no se encola. El monto lo calcula el Satélite; la integración debe poder fijar el precio de la línea sin que SAP lo recalcule con la lista de precios (a confirmar con el proveedor).

**Flujo de consumo:**

```mermaid
sequenceDiagram
    participant APP as Satelite (caso de uso)
    participant DB as PostgreSQL
    participant WRK as Worker Outbox
    participant GW as Agunsa Connect
    participant SAP as SAP B1

    APP->>DB: guarda OFA + outbox_message (misma transaccion)
    WRK->>DB: lee outbox_message PENDIENTE
    WRK->>GW: CrearOrdenVenta(OfaDto) [idempotente]
    GW->>SAP: orden de venta -> factura
    SAP-->>GW: folio + numero interno
    GW-->>WRK: ResultadoEmisionDto
    WRK->>DB: marca ENVIADO; actualiza OFA
    Note over WRK,DB: si falla: reintenta con backoff;<br/>al agotar -> EMITIDA_SIN_CONFIRMACION + alerta
```

---

## 5. Callback de folio / DTE (REQ-009, ADR-002/004)

**Cómo se consume:** el Satélite **expone un endpoint** que Agunsa Connect invoca cuando SAP emite el DTE. Une por **folio**.

```jsonc
// POST /api/sap/callbacks/folio   (entrada)
{
  "ofaId": "a1b2c3d4-...",
  "folio": "F-00098765",
  "tipoDte": 33,                         // 33/34/52/56/61/110/111/112 (RN-034)
  "montoNeto": 30000.0000,
  "montoTotal": 35700.0000,
  "dteXmlBase64": "PD94bWw...",          // XML del DTE (RN-076); PDF no viene por aqui
  "emitidoEn": "2026-10-26T15:10:00Z"
}
```
Al recibirlo: cierra el vínculo operación↔factura por folio y dispara la conciliación (calculado ↔ DTE).

---

## 6. `IAexsaRetornoPort` — cierre del lazo (REQ-009, ADR-004)

**Cómo se consume (revisado 2026-11-05):** Agunsa Connect avisa al Satélite que se facturó (§5). El Satélite concilia y **informa a AEXSA SYS la factura emitida**, para habilitar el check de salida (RN-072) y dejar la trazabilidad completa de que la factura se generó correctamente. El aviso sale por el **Outbox** (misma transacción que la conciliación), con reintentos, registro de solicitud, respuesta y error, alerta al agotarse y reenvío desde la pantalla. AEXSA expone un **endpoint de recepción nuevo** y responde con un acuse. Idempotente por folio. El Satélite no escribe en la base de AEXSA.

```csharp
public interface IAexsaRetornoPort
{
    Task<AcuseAexsaDto> NotificarFacturaEmitidaAsync(RetornoFacturaDto retorno, CancellationToken ct);
}
```
```jsonc
// RetornoFacturaDto (Satélite → AEXSA). Endpoint de AEXSA: [POR CONFIRMAR con el equipo de AEXSA]
{
  "claveIdempotencia": "F-00098765",       // el folio: repetir el aviso no duplica
  "operacionIdAexsa": "98933",             // SolicitudVisadoID recibido en el evento
  "documentoCargaId": "184520",
  "ofaId": "a1b2c3d4-...",
  "folio": "F-00098765",
  "tipoDte": 33,
  "montoNeto": 30000.0000,
  "montoTotal": 35700.0000,
  "moneda": "CLP",
  "fechaEmision": "2026-10-26",
  "dteXmlBase64": "PD94bWw..."             // si está disponible (PA-008)
}

// AcuseAexsaDto (AEXSA → Satélite)
{ "folio": "F-00098765", "resultado": "RECIBIDO",   // RECIBIDO | YA_RECIBIDO | RECHAZADO
  "estadoOperacionAexsa": "FACTURADA", "mensaje": null }
```

- `RECIBIDO` o `YA_RECIBIDO`: la OFA pasa a `INFORMADA_AEXSA`.
- `RECHAZADO` (por ejemplo, operación inexistente o anulada): no se reintenta; alerta para revisión.
- Error de red o 5xx: reintento con espera creciente; al agotarse, alerta y reenvío desde la pantalla.
- `[POR CONFIRMAR]` Si el canal actual `GetFactura` (SAP → AEXSA) se apaga para estas facturas. Recomendación: el Satélite es el único que informa; si conviven, AEXSA ignora el segundo aviso de un mismo folio.

---

## 7. `IExtraccionConvenioPort` — IA asistida (REQ-012, ADR-007)

**Cómo se consume:** request/response contra el servicio de IA. La salida es una **propuesta** con score de confianza; **nunca** deja el convenio vigente por sí sola — requiere confirmación humana.

```csharp
public interface IExtraccionConvenioPort
{
    Task<PropuestaConvenioDto> ExtraerAsync(DocumentoConvenio doc, CancellationToken ct);
}
```
```jsonc
// PropuestaConvenioDto (entra como version_convenio en estado BORRADOR)
{
  "clienteRut": "76.123.456-7",
  "confianza": 0.92,                     // umbral; por debajo -> revision reforzada
  "tcMinimo": 800.0000, "diasLibres": 10,
  "vigenciaDesde": "2026-01-01", "vigenciaHasta": "2026-12-31",
  "servicios": [
    { "tipoServicioId": 42, "tarifa": 15000, "moneda": "CLP", "unidad": "CTR" }
  ]
}
```

---

## 8. Reglas transversales de consumo (todos los contratos)
1. **Idempotencia** por la clave indicada en §1; reenviar no duplica.
2. **Reintentos con backoff** en salidas (Outbox, retorno); tope y alerta al agotar (RNF-07).
3. **Log de request/response/error** por mensaje; **reproceso desde la UI** sin tocar la BD (ADR-002).
4. **Validación de datos antes de transmitir** (completitud de la OFA antes de encolar).
5. **Contrato versionado** (`v1`) independiente del transporte; cambiar el transporte (lo que defina Agunsa Connect, PA-002) no cambia el contrato de datos.
6. **Stub primero:** cada adaptador real tiene un stub equivalente para construir y probar sin el tercero.

## 9. Dependencias abiertas
- **PA-002:** transporte/seguridad real de Agunsa Connect (REST, cola, bus) — fija cómo viaja cada contrato, no su forma.
- **PA-001:** objeto SAP que crea la orden de venta — implementa `ISapOrdenVentaPort`.
- **PA-003/PA-007:** objeto/query de lectura y mapeo RUT→CardCode — implementan `ISapCatalogoPort`.
- **PA-004/PA-006/PA-008:** vías reales de retorno a AEXSA e ingesta del evento.
