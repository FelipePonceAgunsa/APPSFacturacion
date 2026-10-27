# Estado de la arquitectura — Satélite de Facturación AEXSA

Última actualización: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Propósito: punto de control de lo producido hasta aquí, dónde está cada cosa y qué queda pendiente. Permite retomar la sesión sin perder contexto.

---

## 1. Qué está hecho y dónde

### Spec formal — `.kiro/specs/satelite-facturacion/`
| Archivo | Contenido | Autor |
|---------|-----------|-------|
| `requirements.md` | REQ-001…012 (del levantamiento) | Analista |
| `design.md` | Diseño de arquitectura: capas, modelo de dominio, flujo de los 7 pasos, integraciones, trazabilidad REQ→diseño. Diagramas Mermaid. | Arquitecto |
| `tasks.md` | Plan de implementación por fases, mapeado a las entregas E1–E5 (Fase A/B). | Arquitecto |

### Decisiones y planificación de arquitectura — `docs/arquitectura/`
| Archivo | Contenido |
|---------|-----------|
| `adr/ADR-000-indice.md` | Índice de los 11 ADR con su estado. |
| `adr/ADR-001…011` | Decisiones de arquitectura (estilo, integración SAP/Agunsa Connect, lectura tarifas, retorno AEXSA, ingesta evento, motor determinístico, convenios IA, capa configurable, omisiones, modelo de datos, repartido/DUS). |
| `preguntas-arquitectura.md` | PA-001…008: decisiones técnicas que dependen de terceros. |
| `plan-entregas.md` | Priorización por valor: E1–E5, Fase A (sin terceros) / Fase B (con terceros). Documento para presentar a negocio. |
| `estimacion-hh.md` | Esfuerzo en **horas Kiro** (desarrollo dirigido con IA) por entrega. Sin precios. |
| `estado-arquitectura.md` | Este documento. |

---

## 2. Decisiones clave tomadas
- **Arquitectura hexagonal, monolito modular**, .NET 10 + EF Core + PostgreSQL + Angular + AWS (ADR-001).
- **Integración SAP aislada tras puerto + Outbox** para no bloquear el desarrollo con el riesgo de PA-001/PA-002 (ADR-002).
- **Motor de tarifas determinístico y versionado; la IA nunca fija monto** (ADR-006).
- **Convenio vive en el Satélite**, versionado, con extracción IA + confirmación humana (ADR-007).
- **Capa configurable de 3 niveles** para las reglas no escritas (ADR-008).
- **Repartido y DUS como capacidades nuevas** (ADR-011).
- **Priorización por valor:** Fase A (convenios + factura ajustada + omisiones) da valor sin depender de terceros; Fase B (integración SAP + pagos) depende de terceros.
- **Primera fase operativa:** el Satélite entrega la prefactura valorizada y el facturador la digita en SAP como hoy (confirmado con el usuario 2026-10-26).

## 3. Estimación resumida (horas Kiro)
- Fase A (E1+E2+E3 + fundaciones): **33 – 50 horas Kiro**.
- Fase B lado Satélite (E4+E5): **22 – 32 horas Kiro**.
- Total desarrollo con Kiro: **≈ 55 – 82 horas**.
- Integración SAP real, pruebas con negocio, ciberseguridad y producción: **no se cuentan en horas Kiro** (dependen de terceros; se estiman en calendario).

---

## 4. Pendientes abiertos

### Técnicos (dependen de terceros — gestionar en paralelo)
- **PA-001 / PA-002:** vía de escritura en SAP + contrato de Agunsa Connect (bloqueante principal de Fase B).
- **PA-003 / PA-007:** API de lectura de tarifas SAP y mapeo RUT→CardCode (con Marlene).
- **PA-004 / PA-006 / PA-008:** retorno a AEXSA SYS, ingesta del evento, acceso al XML/PDF (con Ricardo).
- **PA-005:** certificación de ciberseguridad y ley de datos personales.

### De negocio
- **PQ-006:** criterio STI vs DPW (única regla de monto abierta, con Francisco).
- **BR-030:** mecánica fina del cobro por DUS.

### Del entregable de arquitectura (opcional, para el F02 completo)
- Contratos de puertos campo a campo (DTOs).
- DDL / migración detallada del modelo de datos.
- Especificación OpenAPI de la API REST.
- Formularios F01 / F02 del procedimiento SGSI-ATI-P03.
- Estrategia de pruebas y de migración de datos.

---

## 5. Nota sobre versionado
`git` no está instalado en el equipo de trabajo (verificado 2026-10-26), por lo que el entregable no está versionado en un repositorio. Todos los archivos están guardados en disco. Al disponer de git (o al mover el trabajo a un equipo que lo tenga), conviene inicializar el repositorio y registrar un commit con este estado como línea base.
