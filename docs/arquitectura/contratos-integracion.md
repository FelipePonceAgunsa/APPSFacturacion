# Contratos de integración — Satélite de Facturación AEXSA

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
| `IEventoFacturablePort` | AEXSA → Satélite | Pull (polling) por defecto; push (webhook) si se habilita | `operacion_id_aexsa` | PA-006 |
| `ISapCatalogoPort` | SAP → Satélite | Consulta (request/response) + snapshot versionado local | versión de snapshot | PA-003/PA-007 |
| `ISapOrdenVentaPort` | Satélite → SAP | **Outbox asíncrono** → Agunsa Connect | `clave_idempotencia` (OfaId+versión) | PA-001/PA-002 |
| (callback de folio) | SAP → Satélite | Callback HTTP a endpoint del Satélite | `folio` | PA-002 |
| `IAexsaRetornoPort` | Satélite → AEXSA | Notificación (reutiliza canal de conciliación) | `folio` | PA-004/PA-008 |
| `IExtraccionConvenioPort` | IA → Satélite | Request/response; salida es **propuesta** con confirmación humana | — | — |

Todos los contratos: transporte y seguridad los fija Agunsa Connect (PA-002); aquí se define el **contrato de datos**, independiente del transporte. Todo intercambio registra request/response/error para auditoría y reproceso (ADR-002).

---

## 2. `IEventoFacturablePort` — entrada del evento (REQ-001, ADR-005)

**Cómo se consume:** el Satélite **consulta** periódicamente (pull) una vista/API de AEXSA que lista operaciones con visado aprobado aún no ingeridas. Si AEXSA habilita webhook (push), el Satélite expone `POST /api/aexsa/eventos` y el pull queda de respaldo. Cada operación se procesa **una sola vez** (idempotencia por `operacionIdAexsa`).

```csharp
public interface IEventoFacturablePort
{
    // pull: trae lo pendiente de ingerir
    Task<IReadOnlyList<EventoFacturableDto>> ObtenerPendientesAsync(CancellationToken ct);
}
```

```jsonc
// EventoFacturableDto  (v2, 2026-11-04). Prioridad: A = obligatorio, B = necesario en casos concretos, C = deseable.
{
  // --- Control del mensaje
  "tipoEvento": "VISADO_APROBADO",        // A · VISADO_APROBADO | OPERACION_ACTUALIZADA
  "version": 1,                           // A · sube con cada actualización; el Satélite actualiza, no duplica
  "operacionIdAexsa": "OP-2026-000123",   // A · clave de idempotencia
  "visadoAprobadoEn": "2026-10-26T14:30:00Z", // A

  // --- Clasificación de la ruta (REQ-002)
  "tipoCarga": "CONTENEDOR",              // A · CONTENEDOR | BULTO (RN-027). BULTO es 2ª etapa: se recibe y se marca fuera de alcance
  "sentido": "IMPORTACION",               // A · IMPORTACION | EXPORTACION (impo usa DIN, expo usa DUS)
  "tipoRetiro": "DIRECTO",                // A · DIRECTO | INDIRECTO (RN-026)
  "conConvenio": true,                    // A · derivado del estado PREAPROBADO_CONVENIO / SIN_CONVENIO (RN-028)
  "terminal": "STI",                      // A · STI | DP_WORLD | OTRO (RN-079). Define la refacturación del directo

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
      "bajoAforo": { "dias": 0, "finalizadoEn": null }, // B · RN-052 / RN-058
      "esReefer": false,                  // C
      "inspecciones": [ { "tipo": "SAG", "finalizadaEn": null } ] // B · RN-054 / RN-058
    }
  ],

  // --- Servicios prestados (base del cálculo y de la detección de omisiones, REQ-007)
  "servicios": [                          // A
    {
      "tipoServicioId": 42,               // A · MasterData.TipoServicio (RN-032)
      "cantidad": 2, "unidad": "CTR",     // A
      "contenedor": "ABCD1234567",        // B
      "finalizadoEn": "2026-10-24T10:00:00Z", // B
      "origen": "AEXSA"                   // C · AEXSA | LIQUIDACION_PUERTO (cargos de puerto, RN-056)
    }
  ]
}
```

**No debe viajar:** los montos tecleados a mano en AEXSA (`MontoFacturaCosto`, `MontoFacturaVenta`), porque el cálculo es del Satélite (RN-025); credenciales; ni datos personales que no estén en la lista.

**Cuándo se envía:** al aprobarse el visado (`VISADO_APROBADO`) y cada vez que cambie algo que afecte el cobro antes de facturar (`OPERACION_ACTUALIZADA`): fecha de despacho o retiro, servicios nuevos, reclasificación directo→indirecto (RN-044), cliente facturador asignado, cargos de puerto tardíos.

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

**Cómo se consume:** el Satélite **notifica** el folio a AEXSA SYS para habilitar el check de salida (RN-072), reutilizando el canal de conciliación por folio. Idempotente por folio.

```csharp
public interface IAexsaRetornoPort
{
    Task NotificarFacturaEmitidaAsync(RetornoFacturaDto retorno, CancellationToken ct);
}
```
```jsonc
// RetornoFacturaDto
{ "operacionIdAexsa": "OP-2026-000123", "folio": "F-00098765",
  "tipoDte": 33, "montoTotal": 35700.0000 }
```

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
