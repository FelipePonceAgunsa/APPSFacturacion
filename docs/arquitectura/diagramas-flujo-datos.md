# Flujo de datos y arquitectura — ZARPE - FACTURACIÓN

Versión: 1.1 (catálogo SAP: análisis previo y extracción en Fase A, API-1 en Fase B)
Fecha: 2026-11-05
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Base: `contratos-integracion.md` (§2 a §6), ADR-002, ADR-005, `arquitectura-despliegue.md`, `modelo-datos.md`.
Propósito: dibujar cómo viajan los datos de AEXSA SYS a ZARPE - FACTURACIÓN y de ahí a SAP, para conversarlo con los desarrolladores. Los diagramas están en Mermaid y se ven en Kiro, GitHub y la mayoría de los editores de Markdown.

> **Qué es Fase A y qué es Fase B.** En la Fase A el sistema recibe el evento de AEXSA SYS y calcula; el facturador factura a mano en SAP. En la Fase B se agrega el envío automático a SAP, el folio de vuelta, los pagos y el retorno a AEXSA SYS. En los diagramas, **línea continua = Fase A**, **línea punteada = Fase B**.

---

## Imágenes listas para abrir o proyectar

Están en `docs/arquitectura/diagramas-uml/`, en PNG (para presentar) y SVG (para ampliar sin perder calidad). Se generan desde los bloques Mermaid de este mismo archivo con `scripts/desarrollo/generar_diagramas_uml.py`; si cambias un diagrama aquí, vuelve a ejecutar el script.

| Imagen | Diagrama UML |
|---|---|
| `01-componentes-flujo-de-datos.png` | Componentes y flujo de datos (vista general) |
| `02-secuencia-aexsa-a-zarpe.png` | Secuencia: de AEXSA a ZARPE (Fase A) |
| `03-secuencia-zarpe-a-sap.png` | Secuencia: de ZARPE a SAP, folio y pagos (Fase B) |
| `04-estados-operacion-y-ofa.png` | Estados de la operación y de la OFA |
| `05-secuencia-catalogo-sap.png` | Secuencia: catálogo de SAP (análisis previo y extracción en Fase A, API-1 en Fase B) |

![Componentes y flujo de datos](diagramas-uml/01-componentes-flujo-de-datos.png)

---

## Qué tipo de diagrama UML usar para cada pregunta

| Pregunta | Diagrama UML | Dónde está aquí |
|---|---|---|
| ¿Qué piezas hay y cómo se conectan? | **Componentes** | Diagrama 1 |
| ¿Quién habla con quién, en qué orden y con qué datos? | **Secuencia** | Diagramas 2 y 3 |
| ¿Por qué estados pasa una operación o una OFA? | **Estados** (máquina de estados) | Diagrama 4 |
| ¿Cómo llegan los datos de SAP al catálogo? | **Secuencia** | Diagrama 5 |
| ¿Dónde corre cada pieza en AWS? | **Despliegue** | `arquitectura-despliegue.md` |
| ¿Qué tablas hay y cómo se relacionan? | **Clases / entidad-relación** | `modelo-datos.md` |

Para conversar con desarrolladores, los más útiles son **Secuencia** (el orden y los datos de cada mensaje) y **Componentes** (el mapa general). Empieza por el Diagrama 1 y luego baja a los de secuencia.

---

## Diagrama 1 — Componentes y flujo de datos (vista general)

Los números de las flechas se explican en la tabla siguiente.

```mermaid
flowchart TB
    VIS["Visador<br/>(usuario AEXSA)"]
    FAC["Facturador<br/>(usuario ZARPE)"]

    subgraph AEXSA["AEXSA SYS (lo mantiene el equipo de AEXSA)"]
        UI_A["Pantalla<br/>Respuesta pre-visado<br/>botón Aprueba"]
        DB_A[("BD AEXSA")]
        OUT_A["Tabla de salida<br/>(outbox)"]
        ENV_A["Proceso de envío<br/>con reintentos"]
        GET_A["GET de solo lectura<br/>(conciliación)"]
        REC_A["Endpoint de recepción<br/>factura emitida (nuevo)"]
        FLU_A["Documento pasa a Facturado<br/>en el flujo de AEXSA"]
    end

    subgraph ZARPE["ZARPE - FACTURACIÓN (AWS)"]
        API["API REST<br/>Satelite.Api"]
        MOTOR["Motor de tarifas<br/>(determinístico)"]
        DB_Z[("PostgreSQL<br/>operaciones, convenios,<br/>OFA, memoria de cálculo")]
        CAT[("Catálogo SAP<br/>versionado")]
        WRK["Worker<br/>despachador Outbox"]
        UI_Z["Pantallas Angular"]
    end

    AC["Agunsa Connect<br/>(middleware corporativo)"]
    SAP["SAP Business One"]
    ARCH["Archivo Excel/CSV<br/>artículos, precios, kits, TC"]

    VIS -->|"1 presiona Aprueba"| UI_A
    UI_A -->|"2 guarda aprobación<br/>+ mensaje, misma transacción"| DB_A
    DB_A --- OUT_A
    OUT_A --> ENV_A
    ENV_A ==>|"3 POST /aexsa/eventos<br/>EventoFacturable (JSON)"| API
    API -->|"4 valida, guarda, busca el<br/>convenio por RUT, calcula pendientes"| DB_Z
    DB_Z <-->|"5 contexto de cálculo"| MOTOR
    CAT -->|"tarifa pública"| MOTOR
    SAP -->|"6a una persona del equipo SAP<br/>exporta desde SAP"| ARCH
    ARCH -->|"6b el administrador lo sube<br/>en la pantalla Catálogo y confirma"| UI_Z
    API --> CAT
    API -.->|"6c API-1 consulta<br/>de catálogo"| AC
    FAC <-->|"7 revisa, ajusta,<br/>agrega servicios"| UI_Z
    UI_Z <--> API
    GET_A -.->|"conciliar eventos"| API

    DB_Z -.->|"8 OFA validada<br/>+ outbox, misma transacción"| WRK
    WRK -.->|"9 Orden de venta<br/>OfaDto idempotente"| AC
    AC -.-> SAP
    SAP -.->|"10 folio + DTE"| AC
    AC -.->|"11 se facturó:<br/>folio + DTE"| API
    WRK -.->|"12 informa factura emitida<br/>(Outbox, idempotente por folio)"| REC_A
    REC_A -.->|"acuse de recibo"| WRK
    REC_A -.-> FLU_A
    AC -.->|"13 estado de pago<br/>por folio"| API
```

### Qué viaja en cada flecha

| N.º | De → A | Qué viaja | Mecanismo | Fase |
|---|---|---|---|---|
| 1 | Visador → AEXSA | Revisión de documentos y aprobación. El visador **no** indica si hay convenio: eso lo determina ZARPE | Pantalla de AEXSA | A |
| 2 | Pantalla → BD AEXSA | La aprobación y el mensaje del evento, **en una sola transacción** | Escritura en BD | A |
| 3 | AEXSA → ZARPE | `EventoFacturable`: operación, documento de carga, tipo de carga, sentido, tipo de retiro, terminal, DIN/DUS, partes, contenedores, servicios prestados. **Sin montos tecleados a mano** | `POST /aexsa/eventos`, con reintentos, idempotente por `operacionIdAexsa` + `version` | A |
| 4 | API → BD | Operación, contenedores, servicios, registro del mensaje (`evento_aexsa`), lista de datos pendientes. **ZARPE busca el convenio vigente** por RUT del cliente facturador (o consignatario) y fecha; si no hay, la operación es sin convenio | Escritura transaccional | A |
| 5 | BD ↔ Motor | `ContextoCalculo` inmutable: operación, **convenio vigente que ZARPE encontró por RUT y fecha** (o ninguno), tarifa pública, tipo de cambio. Sale: líneas con monto y memoria de cálculo | Llamada interna, sin consultas en vivo | A |
| 6a | SAP → archivo | Artículos, listas de precios, kits, tipo de cambio y socios de negocio | Una persona del equipo SAP **exporta** esos datos desde SAP a un Excel/CSV, como cualquier reporte. La primera vez es una **muestra** que se revisa con facturación (EXT-1, análisis previo); después, la extracción completa (EXT-2), y de nuevo cada vez que cambien precios | A |
| 6b | Archivo → ZARPE | Lo mismo. **Puente: solo mientras no exista la API-1 (6c)** | El **administrador de ZARPE sube el archivo** en la pantalla Catálogo. ZARPE lo revisa (formato, duplicados, inactivos), muestra el resultado y la persona confirma. Queda guardado como una versión del catálogo; cada cálculo registra qué versión usó | A |
| 6c | ZARPE → SAP (vía Agunsa Connect) | Lo mismo, por consulta automática de solo lectura. **Reemplaza a 6a y 6b apenas exista** | API-1, programada y a pedido. La **construye el equipo SAP** según `solicitud-apis-sap.md`, sobre lo aprendido en el análisis previo. Se pide desde ya | A si llega a tiempo; si no, B |
| 7 | Facturador ↔ pantallas | Ver cálculo, "¿Por qué?", ajustar monto, agregar servicio, corregir directo/indirecto | HTTPS, rol en la API | A |
| 8 | OFA → Outbox | OFA validada y mensaje de envío, **misma transacción** | Escritura en BD | B |
| 9 | Worker → SAP | `OfaDto`: destinatarios con líneas (artículo SAP, cantidad, tarifa, moneda, monto), clave de idempotencia | Vía Agunsa Connect, reintentos con espera creciente | B |
| 10 | SAP → Agunsa Connect | Folio, número interno, DTE | Respuesta de SAP | B |
| 11 | Agunsa Connect → ZARPE | **Aviso de que se facturó:** `ofaId`, folio, tipo de DTE, montos neto y total, fecha de emisión, XML del DTE. ZARPE une por folio y concilia lo calculado contra lo facturado | `POST /sap/callbacks/folio`, idempotente por folio | B |
| 12 | ZARPE → AEXSA | **Factura emitida**, ya conciliada: `operacionIdAexsa`, `documentoCargaId`, folio, tipo de DTE, montos, fecha de emisión y XML del DTE. AEXSA pasa la operación a **Facturada**, habilita el check de salida y devuelve un **acuse de recibo**. Así queda la trazabilidad completa: AEXSA → ZARPE → SAP → ZARPE → AEXSA | Outbox de ZARPE con reintentos hacia un **endpoint de recepción nuevo en AEXSA** (lo construye el equipo de AEXSA). Idempotente por folio. Reenvío desde la pantalla. ZARPE no escribe en la base de AEXSA | B |
| 13 | SAP → ZARPE (vía Agunsa Connect) | Estado de pago de cada factura, por folio: pagada, parcial o impaga. Un pago puede cubrir varias facturas | Callback o consulta `[POR CONFIRMAR]` (PA-002) | B |

---

## Diagrama 2 — Secuencia: de AEXSA a ZARPE - FACTURACIÓN (Fase A)

```mermaid
sequenceDiagram
    autonumber
    actor VIS as Visador
    participant UIA as AEXSA SYS (pantalla)
    participant BDA as BD AEXSA + tabla de salida
    participant ENV as Proceso de envío AEXSA
    participant API as API ZARPE
    participant BDZ as PostgreSQL ZARPE
    participant MOT as Motor de tarifas
    actor FAC as Facturador

    VIS->>UIA: Revisa que los documentos estén correctos
    VIS->>UIA: Presiona Aprueba
    UIA->>BDA: Guarda aprobación + mensaje (una transacción)
    ENV->>BDA: Lee mensajes sin enviar
    ENV->>API: POST /aexsa/eventos (EventoFacturable, version N)

    alt Mensaje nuevo o versión mayor
        API->>BDZ: Registra en evento_aexsa (APLICADO)
        API->>BDZ: Crea o actualiza operación, contenedores y servicios
        API->>BDZ: Busca el convenio vigente por RUT y fecha
        Note over API,BDZ: ZARPE decide: con convenio (guarda la versión) o sin convenio (tarifa pública).<br/>Si hay dos vigentes: el más antiguo y pendiente VARIOS_CONVENIOS_VIGENTES
        API->>BDZ: Calcula pendientes (tipo de retiro, terminal, DUS, etc.)
        API-->>ENV: 201 aceptado
    else Mensaje repetido
        API-->>ENV: 409 ya recibido (no hace nada)
    else Mensaje inválido
        API-->>ENV: 400 rechazado (sin reintento)
    end

    FAC->>API: Abre la operación
    API->>MOT: ContextoCalculo (operación + convenio + tarifa pública + TC)
    MOT-->>API: Líneas con monto y memoria de cálculo
    API-->>FAC: Total a cobrar y "¿Por qué?" de cada línea

    opt Corrección humana
        FAC->>API: Completa dato, cambia retiro, agrega servicio o ajusta (con motivo)
        API->>BDZ: Registra el cambio en auditoría
        API->>MOT: Recalcula
        MOT-->>API: Nuevo cálculo
    end

    Note over FAC: Fase A: el facturador emite la factura en SAP con estos valores
```

**Qué mirar al conversarlo con el equipo:**
- AEXSA nunca envía montos ni dice si hay convenio. **ZARPE busca el convenio y calcula**: ese es su valor.
- El mensaje es idempotente: repetirlo no duplica. Una versión nueva actualiza la operación.
- Lo que falta no hace fallar el mensaje: queda como pendiente y lo completa una persona, con registro.

---

## Diagrama 3 — Secuencia: de ZARPE - FACTURACIÓN a SAP (Fase B)

```mermaid
sequenceDiagram
    autonumber
    actor FAC as Facturador
    participant API as API ZARPE
    participant BDZ as PostgreSQL ZARPE
    participant WRK as Worker (Outbox)
    participant AC as Agunsa Connect
    participant SAP as SAP B1
    participant AEX as AEXSA SYS

    FAC->>API: Valida la OFA y presiona Enviar a SAP
    API->>BDZ: Guarda OFA ENVIADA + outbox_message (una transacción)
    API-->>FAC: 202 en proceso

    loop Hasta enviar o agotar reintentos
        WRK->>BDZ: Lee outbox_message PENDIENTE
        WRK->>AC: CrearOrdenVenta(OfaDto, claveIdempotencia)
        AC->>SAP: Orden de venta y factura
        SAP-->>AC: Folio y número interno
        AC-->>WRK: ResultadoEmision (EMITIDA)
    end

    alt Respondió bien
        WRK->>BDZ: Marca ENVIADO y OFA EMITIDA
    else Se agotaron los reintentos
        WRK->>BDZ: OFA EMITIDA_SIN_CONFIRMACION
        WRK-->>FAC: Alerta (reenvío desde la pantalla, sin tocar la BD)
    end

    SAP->>AC: DTE emitido
    AC->>API: Se facturó: POST /sap/callbacks/folio (folio, tipoDte, montos, XML)
    API->>BDZ: Une por folio y concilia calculado vs DTE
    API->>BDZ: OFA CONCILIADA + outbox_message RETORNO_AEXSA (una transacción)

    loop Hasta recibir acuse o agotar reintentos
        WRK->>BDZ: Lee outbox_message RETORNO_AEXSA
        WRK->>AEX: Factura emitida (operacionIdAexsa, folio, tipoDte, montos, XML) [idempotente por folio]
        AEX-->>WRK: Acuse de recibo
    end
    Note over AEX: La operación pasa a Facturada en el flujo de AEXSA

    alt Acuse recibido
        WRK->>BDZ: OFA INFORMADA_AEXSA (queda la traza completa)
    else Se agotaron los reintentos
        WRK-->>FAC: Alerta: factura emitida sin informar a AEXSA (reenvío desde la pantalla)
    end

    Note over SAP,API: Más adelante, cuando el cliente paga
    SAP->>AC: Pago registrado (folios que cubre)
    AC->>API: Estado de pago por folio
    API->>BDZ: Registra el pago: pagada, parcial o impaga
    API-->>FAC: Alerta si queda impaga
```

**Qué mirar al conversarlo con el equipo:**
- Nunca se llama a SAP en línea desde la pantalla: se escribe en el Outbox y un Worker lo envía.
- La clave de idempotencia (`OfaId` + versión) evita crear dos órdenes si se reintenta.
- Cada línea lleva el artículo SAP. Una OFA con una línea sin artículo no pasa la validación.
- `[POR CONFIRMAR]` Cómo se escribe en SAP y cómo fija el precio sin que SAP lo recalcule (PA-001, PA-002). Hasta entonces se construye contra un simulador.
- **Agunsa Connect avisa a ZARPE que se facturó, y ZARPE le informa a AEXSA.** ZARPE es el único que informa a AEXSA, después de conciliar. Así la trazabilidad es una sola cadena: AEXSA envía a facturar → ZARPE calcula → SAP factura → ZARPE concilia → AEXSA recibe y confirma.
- El aviso a AEXSA sale por el Outbox de ZARPE, con reintentos, acuse de recibo y reenvío desde la pantalla, igual que el envío a SAP. ZARPE no escribe en la base de AEXSA: AEXSA expone un endpoint de recepción nuevo.
- El folio es la clave que une operación, OFA, factura, aviso a AEXSA y pago.
- `[POR CONFIRMAR]` Con el equipo de AEXSA: el endpoint de recepción y su autenticación. Y si el canal actual `GetFactura` (SAP → AEXSA) se apaga para estas facturas o se mantiene. Recomendación: que ZARPE sea el único que informa, para evitar avisos duplicados; si se mantienen ambos, AEXSA debe ignorar el segundo aviso del mismo folio.
- `[POR CONFIRMAR]` Las operaciones con convenio no se facturan dentro del flujo de AEXSA (se consolidan fuera, PA-010). Falta definir qué pasa con su estado en AEXSA.

---

## Diagrama 4 — Estados de la operación y de la OFA

```mermaid
stateDiagram-v2
    direction LR

    state "Operación" as OP {
        [*] --> Recibida: llega el evento
        Recibida --> ConPendientes: faltan datos
        Recibida --> Calculada: sin pendientes
        ConPendientes --> Calculada: la persona completa el dato
        Calculada --> Calculada: llega versión nueva o corrección
        Calculada --> Anulada: AEXSA envía ANULADA
        ConPendientes --> Anulada: AEXSA envía ANULADA
        Anulada --> [*]
    }

    state "OFA" as OFA {
        [*] --> BORRADOR
        BORRADOR --> VALIDADA: completa y sin pendientes
        VALIDADA --> BORRADOR: se corrige y se recalcula
        VALIDADA --> ENVIADA: Enviar a SAP (Fase B)
        ENVIADA --> EMITIDA: SAP responde folio
        ENVIADA --> EMITIDA_SIN_CONFIRMACION: se agotan reintentos
        EMITIDA_SIN_CONFIRMACION --> ENVIADA: reenvío desde pantalla
        EMITIDA --> CONCILIADA: DTE coincide con el cálculo
        CONCILIADA --> INFORMADA_AEXSA: AEXSA acusa recibo
        INFORMADA_AEXSA --> [*]
    }
```

Notas:
- La OFA ya enviada no se recalcula: una corrección debe hacerse antes de enviar.
- Un `VARIOS_CONVENIOS_VIGENTES` o un `DOCUMENTO_CON_VARIAS_SOLICITUDES` exige una decisión registrada antes de pasar a `VALIDADA`.

---

## Diagrama 5 — Secuencia: catálogo de SAP (Fase A por extracción, Fase B por API)

```mermaid
sequenceDiagram
    autonumber
    participant EQ as Equipo SAP
    participant SAP as SAP B1
    actor FAC as Facturación
    actor ADM as Administrador ZARPE
    participant API as API ZARPE
    participant BDZ as PostgreSQL ZARPE
    participant AC as Agunsa Connect

    rect rgb(245, 245, 245)
    Note over EQ,BDZ: Fase A · Paso 1: análisis previo de los datos (EXT-1)
    EQ->>SAP: Extrae una muestra (artículos, precios, kits, TC, clientes)
    EQ->>FAC: Revisan juntos la muestra
    Note over EQ,FAC: Significado de cada campo, duplicados, inactivos,<br/>armado de kits, listas vigentes, moneda, tipo de cambio, RUT
    FAC-->>EQ: Acta de hallazgos y ajustes acordados
    end

    rect rgb(245, 245, 245)
    Note over EQ,BDZ: Fase A · Paso 2: extracción para pruebas (EXT-2)
    EQ->>SAP: Extracción completa con los campos acordados
    EQ->>ADM: Entrega el Excel/CSV por canal interno
    ADM->>API: Sube el archivo
    API->>API: Valida formato, duplicados y artículos inactivos
    API-->>ADM: Resultado de la validación (pendiente de confirmar)
    ADM->>API: Confirma
    API->>BDZ: Nueva versión del catálogo (snapshot)
    Note over API,BDZ: Cada cálculo guarda qué versión usó.<br/>Se repite cada vez que cambien precios
    end

    Note over EQ,AC: Fase B · API-1 (se construye en paralelo, sobre el análisis previo)
    API->>AC: Consulta de catálogo (programada o a pedido), solo lectura
    AC->>SAP: Lee artículos, precios, kits, TC y clientes
    SAP-->>AC: Datos
    AC-->>API: Datos
    API->>API: Misma validación que la extracción
    API->>BDZ: Nueva versión del catálogo (snapshot)
```

**Qué mirar al conversarlo con el equipo:**
- El cálculo nunca lee SAP en vivo: usa la versión guardada del catálogo, venga del archivo o de la API.
- El análisis previo se hace **una vez, antes** de construir la carga definitiva. Sus hallazgos fijan los campos de la extracción y el contrato de la API-1.
- La extracción y la API entregan los mismos campos, así que pasar de la Fase A a la B solo cambia el conector, no el cálculo ni las pantallas.
- `[POR CONFIRMAR]` Quién del equipo SAP hace la extracción y cada cuánto cambian los precios.

---

## Diagrama 6 — Dónde corre cada pieza (despliegue en AWS)

Ver `arquitectura-despliegue.md`, §1. En resumen: Angular en S3 + CloudFront (con WAF), API y Worker en ECS Fargate detrás de un balanceador, PostgreSQL en RDS Multi-AZ, documentos en S3 y secretos en Secrets Manager. Toda comunicación con SAP pasa por Agunsa Connect.

---

## Puntos abiertos que afectan estos dibujos

| Punto | Qué cambia en el dibujo |
|---|---|
| PA-002: cómo se conecta Agunsa Connect | Las flechas 9, 10 y 11: transporte, seguridad y quién llama a quién |
| Autenticación AEXSA → ZARPE `[POR CONFIRMAR]` | La flecha 3: si va directa o por Agunsa Connect |
| PA-001: cómo se crea la orden en SAP | La flecha 9: el contenido exacto de lo que se envía |
| Endpoint de recepción en AEXSA `[POR CONFIRMAR]` (PA-004) | La flecha 12: nombre, autenticación y acuse del endpoint que construye el equipo de AEXSA; si se apaga `GetFactura` para estas facturas; qué pasa con las operaciones con convenio (consolidadas, PA-010) |
| Aviso de pagos a ZARPE `[POR CONFIRMAR]` | La flecha 13: cómo se entera ZARPE de un pago (PA-002, PA-003) |
| Análisis previo y extracción de SAP (EXT-1, EXT-2) `[POR CONFIRMAR]` | Las flechas 6a y 6b: quién extrae, con qué frecuencia y qué campos quedan tras el análisis |
| PA-003 y PA-007: API-1 de consulta | La flecha 6c: objeto de SAP que se consulta y cómo se identifica al cliente por RUT |
| Dónde está alojado AEXSA SYS | La red entre AEXSA y AWS (flecha 3) |
