# Restricciones técnicas

Última actualización: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Propósito: consolidar las restricciones técnicas conocidas. Lo confirmado se registra con su fuente; lo que falta se enlaza a la pregunta de arquitectura (PA) que lo resuelve. Las PA viven en `preguntas-arquitectura.md`.

| Tema | Valor | Fuente / responsable |
|------|-------|----------------------|
| **Stack del Satélite** | .NET 10 (LTS) + EF Core, API REST, Angular | **Confirmado** — estándar corporativo (ADR-001) |
| **Base de datos del Satélite** | PostgreSQL | **Confirmado** — estándar corporativo (ADR-001, modelo-datos.md) |
| **Hosting del Satélite** | AWS (ECS Fargate, RDS, S3, CloudFront, WAF) | **Confirmado** — estándar corporativo (arquitectura-despliegue.md) |
| **CI/CD** | GitHub Actions + ECR | **Confirmado** — estándar corporativo |
| **Canal de integración corporativo** | Agunsa Connect (middleware obligatorio) | **Confirmado** — RNF-05; detalle técnico abierto → **PA-002** |
| **Versión de SAP Business One** | [POR CONFIRMAR] | TI-SAP → **PA-001** |
| **Base de datos de SAP B1 (SQL Server / HANA)** | [POR CONFIRMAR] | TI-SAP → PA-001 |
| **SAP B1 on-premise o nube (partner)** | [POR CONFIRMAR] | TI-SAP → PA-001 |
| **Service Layer habilitado** | [POR CONFIRMAR] — define la vía de escritura de la OFA | TI-SAP → **PA-001** |
| **Partner SAP B1** | [POR CONFIRMAR] — probable proveedor externo (AEXSA no tiene desarrollo interno SAP) | TI-SAP → PA-001 |
| **Lectura de artículos/lista de precios SAP** | Viable por API de consulta; objeto/query exacto por confirmar | Marlene Bozzo → **PA-003** |
| **Mapeo cliente RUT → CardCode SAP** | Por confirmar (FederalTaxID) | TI-SAP → **PA-007** |
| **Emisión de DTE (localización / proveedor)** | SAP emite el DTE; el XML vive en software de facturación electrónica aparte | RN-062; acceso al XML/PDF → **PA-008** |
| **Tecnología de AEXSA SYS** | .NET 4.6/4.8 + SQL Server (legacy) | **Confirmado** — levantamiento (respuestas-codigo-aexsa-sys) |
| **Acceso a AEXSA SYS (API, BD, eventos)** | No publica eventos; ingesta por pull a vista/API, push si se habilita | ADR-005 → **PA-006** |
| **Canal de escritura hacia SAP (crear OFA/orden de venta)** | NO existe; es desarrollo nuevo | **Confirmado en código** — RN-076, BR-027 → **PA-001** |
| **Retorno a AEXSA SYS (check de salida)** | Reutiliza canal de conciliación por folio `GetFactura` | RN-074 → **PA-004** |
| **Volumen de eventos facturables / documentos de venta** | ~2.428 documentos de venta/mes (2025) | **Confirmado** — RNF-04, recaudación 2026-10-22 |
| **Volumen de pagos** | ~1.468 pagos/mes (un pago agrupa varias facturas) | **Confirmado** — RNF-04, PQ-011 |
| **Autenticación de usuarios** | [POR CONFIRMAR] (Microsoft 365 / AD corporativo) | TI corporativa |
| **Restricciones de seguridad / datos personales** | Cumplir nueva ley de datos personales; paso por ciberseguridad antes de prod | **Confirmado** — BR-021 → **PA-005** |
| **Secretos / credenciales** | En AWS Secrets Manager; nunca en código (corrige antipatrón BR-016) | **Confirmado** — arquitectura-despliegue.md |
| **Pasarela de pago Khipu** | En código pero DESACTIVADA; no se diseña salvo reactivación | **Confirmado** — RN-075 |

## Notas
- Lo marcado `[POR CONFIRMAR]` ya está trazado a una PA (ver `preguntas-arquitectura.md`); no son huecos sueltos.
- El bloqueante técnico principal es **PA-001 + PA-002** (escritura en SAP + contrato de Agunsa Connect). El resto del diseño avanza contra stubs hasta su confirmación (ADR-002).
