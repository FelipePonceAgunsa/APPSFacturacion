# Diseño — ZARPE - FACTURACIÓN

Versión: 1.1
Fecha: 2026-11-04
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Entrada: `requirements.md` v1.1 (REQ-001…REQ-012), catálogos del levantamiento (RN, BR, PQ), principios de arquitectura (steering). Para desarrollo: `docs/desarrollo/requerimientos.md` y `docs/desarrollo/reglas-negocio.md`.
Decisiones: ver ADRs en `docs/arquitectura/adr/` (ADR-001…ADR-017). Las preguntas técnicas abiertas (PA-001…PA-009) se resumen en `docs/desarrollo/GUIA-DESARROLLADOR.md`.

> **Cambios de la v1.1 (2026-10-28 al 2026-11-04):** (1) autenticación con Cognito federado a Entra ID (ADR-016); (2) catálogo de SAP por archivo en la Fase A, con kits y vínculo servicio↔artículo (ADR-017); (3) los convenios se ingresan por **formulario manual**, y la extracción por IA pasa a **etapa opcional** (ADR-007, ADR-012); (4) regla del directo STI/DP World cerrada (ADR-015); (5) entregas agrupadas en **Fase A** y **Fase B**; (6) la ingesta mínima del evento y las pantallas base pasan a la Fase A.

> Este documento define el **CÓMO**. No reabre decisiones de negocio cerradas (handoff §2). Donde una pieza depende de un tercero (SAP, Agunsa Connect, AEXSA SYS, seguridad), se diseña detrás de un **puerto** para que el Satélite se construya y pruebe sin esperar a esa resolución, y la dependencia queda marcada como PA en estado *Propuesto*.

---

## 1. Resumen ejecutivo

El Satélite es un sistema nuevo que centraliza los eventos facturables de AEXSA SYS, calcula los montos de forma **determinística y auditable**, arma una **OFA valorizada** (orden de venta de SAP), cierra el lazo devolviendo el folio a AEXSA SYS para liberar la carga, y da trazabilidad extremo a extremo para **detectar omisiones de cobro**.

Dos principios gobiernan el diseño:
1. **El monto nunca lo fija la IA.** El cálculo es un motor de reglas determinístico y versionado (ADR-006). La IA, **cuando se active como etapa opcional**, solo extrae, propone y alerta, con confirmación humana (ADR-007, ADR-008, ADR-009). El sistema funciona completo sin IA.
2. **Manda el negocio sobre el legacy.** Repartido (RN-077) y cobro por DUS (RN-047) son capacidades nuevas a construir (ADR-011).

El mayor riesgo técnico es la **escritura hacia SAP** (no existe canal, RN-076/BR-027/PQ-029) y su gobierno por **Agunsa Connect** (BR-028/PQ-030). El diseño lo aísla con el patrón Outbox + adaptadores (ADR-002) de modo que ese riesgo no bloquee la construcción del resto.

---

## 2. Arquitectura general

Arquitectura hexagonal (puertos y adaptadores), desplegada como servicio modular en AWS (ADR-001).

```mermaid
flowchart TB
    subgraph ext[Sistemas externos]
        AEXSA[AEXSA SYS<br/>legacy .NET/SQL Server]
        SAP[SAP Business One]
        FE[Facturacion electronica<br/>XML/PDF DTE]
        IA[Servicio de IA<br/>extraccion / deteccion]
    end

    subgraph connect[Agunsa Connect - middleware corporativo]
        GW[Gobierno de integracion<br/>seguridad / arquitectura]
    end

    subgraph sat[Satelite de Facturacion]
        subgraph api[API REST + Frontend Angular]
            UI[UI comercial / facturacion / recaudacion]
        end
        subgraph app[Capa de aplicacion - casos de uso]
            UC[Orquestacion de los 7 pasos]
            PORTS[[Puertos: ISapOrdenVenta, ISapCatalogo,<br/>IEventoFacturable, IAexsaRetorno,<br/>IExtraccionConvenio, IDeteccionOmisiones]]
        end
        subgraph dom[Dominio - determinista, sin infraestructura]
            MOTOR[Motor de tarifas<br/>pipeline de reglas puras]
            CONV[Convenios versionados]
            REGLAS[Reglas configurables]
            OFA[OFA / destinatarios]
        end
        subgraph infra[Infraestructura - adaptadores]
            DB[(PostgreSQL / EF Core)]
            OUTBOX[Outbox + despachador]
            S3[(S3 documentos)]
            ADAP[Adaptadores: AgunsaConnectSap,<br/>AexsaEvento/Retorno, IA]
        end
    end

    AEXSA -->|evento visado - pull/push| ADAP
    ADAP --> UC
    UC --> MOTOR
    MOTOR --> OFA
    OFA --> OUTBOX
    OUTBOX --> GW --> SAP
    SAP -->|folio / DTE XML - callback/conciliacion| GW --> ADAP
    ADAP -->|retorno folio| AEXSA
    ADAP -->|lectura tarifas / articulos| GW
    IA -.propone.-> ADAP
    FE -.XML/PDF.-> ADAP
    UI --> UC
    app --> DB
    app --> S3
```

### 2.1 Capas
- **Dominio:** motor de tarifas (ADR-006), convenios (ADR-007), reglas configurables (ADR-008), OFA y destinatarios (ADR-011). Determinístico, sin dependencias de infraestructura, cubierto por pruebas.
- **Aplicación:** casos de uso que orquestan los 7 pasos; define los **puertos** hacia el exterior.
- **Infraestructura:** EF Core/PostgreSQL, Outbox + despachador, S3, adaptadores de Agunsa Connect/AEXSA/IA.
- **API + Frontend:** REST para Angular (comercial, facturación, recaudación) y para callbacks de integración.

### 2.2 Despliegue (AWS)
ECS Fargate (API + worker de Outbox), RDS PostgreSQL, S3 (documentos), CloudFront + WAF (frontend), GitHub Actions + ECR (CI/CD). Comunicación con SAP/AEXSA por Agunsa Connect (RNF-05).

---

## 3. Modelo de dominio

Centrado en la **Operación**; versionado y auditable (ADR-010).

```mermaid
classDiagram
    class Cliente {
        Rut
        CardCodeSap
        CondicionPago
        ListaPreciosId
    }
    class Operacion {
        OperacionIdAexsa
        TipoCarga
        TipoRetiro
        ConConvenio
        Puerto
        Din
        CantidadDus
        RutaClasificada
        MomentoFacturacion
    }
    class ServicioPrestado {
        TipoServicioId
        Cantidad
        Unidad
    }
    class Convenio {
        Rut
        Comercial
    }
    class VersionConvenio {
        Numero
        VigenciaDesde
        VigenciaHasta
        Estado
        TcMinimo
        DiasLibres
    }
    class ServicioConvenio {
        TipoServicioId
        Tarifa
        Unidad
        Moneda
    }
    class TipoServicio {
        TipoServicioId
        Codigo
        UnidadNormalizada
        Habilitado
    }
    class TarifaSnapshot {
        Version
        Fecha
        ArticuloId
        Precio
    }
    class ReglaConfigurable {
        Condicion
        Accion
        Version
        Vigencia
    }
    class Ofa {
        Estado
        FechaCalculo
        FolioIdempotencia
    }
    class LineaOfa {
        TipoServicioId
        Cantidad
        Unidad
        Tarifa
        Moneda
        Monto
        EsExcepcion
    }
    class AsignacionCobro {
        DestinatarioRut
    }
    class MemoriaCalculo {
        ReglaVersion
        VersionConvenio
        VersionSnapshot
        TipoCambio
        AjusteManual
    }
    class Factura {
        Folio
        TipoDte
        Montos
        DteXml
    }
    class Pago {
        EstadoPago
        FechaConciliacion
    }
    class EventoAuditoria {
        Usuario
        Fecha
        Accion
        ValorAnterior
        ValorNuevo
    }

    Cliente "1" --> "*" Operacion
    Operacion "1" --> "*" ServicioPrestado
    Operacion "1" --> "1" Ofa
    Convenio "1" --> "*" VersionConvenio
    VersionConvenio "1" --> "*" ServicioConvenio
    Ofa "1" --> "*" LineaOfa
    Ofa "1" --> "*" AsignacionCobro
    LineaOfa "1" --> "1" MemoriaCalculo
    Ofa "1" --> "*" Factura
    Factura "1" --> "*" Pago
    ServicioConvenio ..> TipoServicio
    LineaOfa ..> TipoServicio
    LineaOfa ..> TarifaSnapshot
```

Notas:
- **Identidad por RUT** (BR-020); mapeo a `CardCode` de SAP (RN-065).
- **MemoriaCalculo** y **EventoAuditoria** son **append-only** (reconstrucción del "por qué", REQ-011).
- **Folio** es la clave de cuadratura operación↔OFA↔factura↔pago (RNF-02, RN-029).
- Una Operación puede tener **varias Facturas** (repartido/complementarias, ADR-011).

---

## 4. Flujo extremo a extremo (los 7 pasos)

```mermaid
sequenceDiagram
    participant AEXSA as AEXSA SYS
    participant SAT as Satelite
    participant MOTOR as Motor de tarifas
    participant OUT as Outbox
    participant GW as Agunsa Connect
    participant SAP as SAP B1

    Note over AEXSA,SAT: Paso 1 - evento facturable (REQ-001/002/003)
    AEXSA->>SAT: visado aprobado (pull/push)
    SAT->>SAT: valida DIN vs papeleta; clasifica ruta; destinatario(s)
    Note over SAT: Paso 2 - convenio vigente (REQ-004/012)
    SAT->>SAT: resuelve VersionConvenio vigente (fuente: Satelite)
    Note over SAT,MOTOR: Paso 3 - calculo determinista (REQ-005/006/007)
    SAT->>MOTOR: ContextoCalculo (operacion + convenio + snapshot + TC)
    MOTOR-->>SAT: lineas valorizadas + MemoriaCalculo
    SAT->>SAT: deteccion de omisiones (propone faltantes)
    Note over SAT: Paso 4 - arma y valida OFA (REQ-008)
    SAT->>SAT: OFA valorizada (multi-destinatario); valida completitud
    SAT->>OUT: encola OFA (misma transaccion)
    Note over OUT,SAP: Paso 5 - emision (REQ-009)
    OUT->>GW: crear orden de venta (idempotente, reintentos)
    GW->>SAP: orden de venta -> factura
    SAP-->>GW: folio + DTE
    GW-->>SAT: callback folio / DTE XML
    SAT->>SAT: cierra vinculo operacion-factura por folio
    SAT->>AEXSA: retorna folio -> habilita check de salida (RN-072)
    Note over SAT,SAP: Paso 6 - pago (REQ-010)
    SAP-->>SAT: Pago Recibido conciliado (estado por folio)
    Note over SAT: Paso 7 - historico y auditoria (REQ-011)
    SAT->>SAT: registra historico reconstruible; KPIs
```

### 4.1 Cuadraturas obligatorias (principios)
En cada tramo el Satélite cuadra y, ante desbalance, alerta:
- eventos AEXSA SYS ↔ operaciones en el Satélite (ingesta, ADR-005);
- operación ↔ OFA (toda operación facturable produce OFA o queda marcada no facturable);
- OFA ↔ documentos SAP (toda OFA enviada termina con folio o error explícito, RNF-07);
- factura ↔ pago (conciliación por folio, REQ-010);
- prestado ↔ facturado (detección de omisiones, ADR-009).

---

## 5. Diseño por requerimiento (trazabilidad REQ → diseño)

| REQ | Cómo se diseña | ADR |
|-----|----------------|-----|
| REQ-001 Evento facturable | Puerto `IEventoFacturable` (pull por defecto, push si viable); valida DIN↔papeleta; clasifica momento por convenio | ADR-005 |
| REQ-002 Clasificación de ruta | Regla de clasificación {carga}×{retiro}×{convenio} en el dominio; recalcula si directo→indirecto | ADR-006 |
| REQ-003 Destinatario(s) | `AsignacionCobro` multi-destinatario; default consignatario DIN; registra origen de instrucción; RUT de la DIN | ADR-011 |
| REQ-004 Convenio vigente | Convenio versionado como fuente (ingreso por formulario manual); multi-convenio → más antiguo + override; alerta vencimiento | ADR-007, ADR-012 |
| REQ-005 Cálculo determinístico | Pipeline de reglas puras; USD→CLP con piso; días libres/aforo; recargos; mayor m³/ton; DUS/BL; guarda versiones | ADR-006, ADR-011 |
| REQ-006 Capa configurable | 3 niveles (código/datos/ajuste manual) + promoción; ajuste auditable; IA propone | ADR-008 |
| REQ-007 Detección de omisiones | Cruce determinístico prestado↔valorizado (núcleo); propuesta por IA como etapa opcional; alerta complementarias tardías | ADR-009 |
| REQ-008 OFA hacia SAP | OFA valorizada + validación de completitud; bloquea si incompleta; entrega por Outbox | ADR-002, ADR-010 |
| REQ-009 Emisión y folio | Outbox→Agunsa Connect→SAP; callback de folio; retorno a AEXSA; emitida-sin-confirmación + alerta | ADR-002, ADR-004 |
| REQ-010 Pago y conciliación | Recibe estado de pago por folio; soporta pago agrupado multi-factura/multi-cliente | ADR-004, ADR-010 |
| REQ-011 Histórico y auditoría | `MemoriaCalculo` + `EventoAuditoria` append-only; reconstrucción por versión | ADR-010 |
| REQ-012 Convenio en el Satélite | **Formulario manual** (por defecto) con confirmación humana; versión y vigencia; histórico. Extracción por IA: etapa opcional | ADR-007, ADR-012 |

---

## 6. Integraciones (contratos y resiliencia)

Cada integración define disparador, contrato, idempotencia, reintentos, errores, reproceso y auditoría (principios).

| Integración | Dirección | Puerto | Mecanismo | Idempotencia | Estado |
|-------------|-----------|--------|-----------|--------------|--------|
| Evento facturable | AEXSA → Satélite | `IEventoFacturable` | Pull a vista/API (push si hay webhook) | por `operacionId` | PA-006 |
| Catálogo SAP — **Fase A** | SAP → Satélite | `ISapCatalogoPort` (adaptador de archivo) | Excel/CSV exportado por SAP, validado y confirmado por una persona → snapshot versionado | por versión de snapshot | ADR-017 (sin API) |
| Lectura tarifas — **Fase B** | SAP → Satélite | `ISapCatalogoPort` (adaptador de API) | API consulta + snapshot versionado; reemplaza al archivo | sync idempotente | PA-003, PA-007 |
| Escritura OFA | Satélite → SAP | `ISapOrdenVenta` | Outbox → Agunsa Connect → SAP | por `OfaId`+versión | PA-001, PA-002 |
| Folio/DTE | SAP → Satélite | (callback) | Callback / conciliación por folio | por folio | PA-002 |
| Retorno AEXSA | Satélite → AEXSA | `IAexsaRetorno` | Reutiliza canal de conciliación por folio | por folio | PA-004, PA-008 |
| Extracción convenio *(opcional)* | IA | `IExtraccionConvenio` | Propuesta + confirmación humana. Etapa opcional: el convenio se ingresa por formulario manual | — | — |

Resiliencia (RNF-07): toda OFA enviada termina con **folio o error explícito**; reintentos con backoff; `EMITIDA_SIN_CONFIRMACION` + alerta al agotar; reproceso desde la interfaz sin tocar la BD; log de request/response/error por mensaje.

---

## 7. Seguridad y cumplimiento

### 7.1 Autenticación y autorización (ADR-016)
- **Entra ID** es el proveedor de identidad; **Cognito** es el intermediario que emite los tokens. Por ahora se usa el user pool existente de la empresa, con un **app client propio** del Satélite.
- **Frontend Angular:** flujo Authorization Code con PKCE contra Cognito. No maneja contraseñas.
- **API (.NET):** solo valida el JWT: firma (JWKS), emisor, audiencia o `client_id`, y expiración. No emite tokens ni guarda credenciales.
- **Roles:** Comercial, Facturación, Recaudación y Administrador. Se aplican **en la API**, no solo en los menús. Las acciones sensibles exigen rol explícito (confirmar convenio, ajuste manual, promover regla, reenviar OFA, confirmar catálogo).
- **Pool compartido:** un token válido no basta. La API rechaza tokens de otro app client y niega el acceso a usuarios del pool sin rol del Satélite.
- **Auditoría:** la identidad del token (`sub` y correo) se registra como `usuario` en `evento_auditoria` y en cada ajuste manual.
- **Callbacks de integración** (Agunsa Connect → Satélite) no usan este flujo: son máquina a máquina y dependen de PA-002.
- Pendiente (PA-009): app client y grupos de roles, y si los roles salen de grupos o de app roles de Entra.

### 7.2 Datos y secretos
- Datos personales de clientes bajo la nueva ley; paso por ciberseguridad antes de implementar (BR-021, PA-005). Cifrado en reposo (RDS) y en tránsito; control de acceso por rol.
- **Nunca datos reales de clientes en el repositorio, en las pruebas ni en los ejemplos.** Usar datos sintéticos.
- No heredar el antipatrón de credenciales en claro del legacy (BR-016): secretos en AWS Secrets Manager.
- Toda la comunicación con SAP/AEXSA pasa por Agunsa Connect y su gobierno (RNF-05).

---

## 8. Riesgos y dependencias abiertas (resumen)
Los bloqueantes son de terceros, no del desarrollo del Satélite (principio: separar lo que la IA/desarrollo acelera de lo que no):
- **PA-001/PA-002 (crítico):** vía de escritura en SAP y contrato de Agunsa Connect. Mitigado por Outbox + adaptador simulado: el Satélite se construye y prueba sin ello.
- **PA-003/PA-007:** objeto exacto de lectura SAP y mapeo RUT→CardCode. Mitigado por snapshot + stub.
- **PA-004/PA-006/PA-008:** vía de retorno a AEXSA, ingesta del evento, acceso al XML/PDF. Mitigado por puertos.
- **Negocio (no bloquea):** STI vs DP World (PQ-006) quedó **cerrada** como regla fija por puerto (ADR-015). Sigue abierta la mecánica fina del DUS (BR-030): se absorbe con la capa configurable (ADR-008) y la captura manual de `CantidadDus` hasta confirmarse.
- **Autenticación (PA-009):** falta el app client del Satélite y los grupos de roles en el pool de Cognito. Mitigado con autoridad y audiencia configurables y autenticación de prueba (tarea 0.4).
- **Catálogo SAP en la Fase A (PQ-025):** depende de que el equipo de SAP entregue los archivos A1 a A4. Mitigado con archivos de ejemplo con el formato documentado (`docs/desarrollo/formato-archivos-catalogo-sap.md`).

---

## 9. Alcance de construcción

### 9.1 Por etapa del negocio
- **1ª etapa:** contenedor FULL con/sin convenio; los 7 pasos; OFA→SAP (con adaptador simulado hasta PA-001/PA-002); convenios en el Satélite; cálculo determinístico; capa configurable; detección de omisiones (cruce determinístico); repartido. DUS modelado, activación según alcance de expo.
- **2ª etapa:** carga LCL/suelta (letras A–F), expo por DUS a volumen, licitaciones, gestión contable de notas de crédito/devoluciones (se registran, no se calculan).

### 9.2 Por fase de entrega dentro de la 1ª etapa
| Fase | Qué incluye | Qué entrega al negocio | Terceros |
|---|---|---|---|
| **A** | Fundaciones y autenticación; convenios por formulario manual; catálogo SAP por archivo; motor de tarifas; reglas configurables y ajuste manual del facturador; OFA validada y "lista para digitar"; ingesta mínima del evento; detección de omisiones; pantallas base | Convenio único y vigente, prefactura valorizada que el facturador digita en SAP como hoy, y alerta de cobros omitidos | Archivos de catálogo de SAP y app client de Cognito |
| **B** | Escritura automática en SAP (Outbox), folio y DTE, retorno a AEXSA SYS, conciliación de pagos, ingesta real, cuadraturas, seguridad | Fin de la digitación en SAP y trazabilidad de la operación al pago | SAP, Agunsa Connect, AEXSA SYS, ciberseguridad |

### 9.3 Etapa opcional (se construye solo si el negocio la pide)
Extracción de convenios desde PDF o Word con IA (ADR-007, ADR-012), vínculo servicio↔artículo propuesto por IA (ADR-017) y propuesta de omisiones por IA (ADR-009). El sistema funciona completo sin ellas.

Ver el plan de implementación en `tasks.md` y la guía de arranque en `docs/desarrollo/GUIA-DESARROLLADOR.md`.
