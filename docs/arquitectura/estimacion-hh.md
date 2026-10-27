# Estimación de esfuerzo — Satélite de Facturación AEXSA

Versión: 2.0
Fecha: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Base: `plan-entregas.md` v1.0, `tasks.md` v1.0, medición de productividad con Kiro (caso AEXSA).
Propósito: estimar el esfuerzo por entrega en **horas de desarrollo dirigido con Kiro (IA)**. Unidad = horas de trabajo de una persona dirigiendo a Kiro, NO horas-hombre de desarrollo tradicional. Sin precios.

> Cambio v2.0: la estimación se expresa en **horas Kiro** (trabajo dirigido con IA), no en HH tradicionales ni en costos. Se separa lo que Kiro acelera (desarrollo de código) de lo que no acelera (integración con terceros, pruebas con negocio, producción).

---

## 1. Qué mide "hora Kiro" y de dónde sale

**Referencia medida (caso AEXSA):** el piloto **Gate In** —70 SP, paridad 1:1 con el legacy (33 columnas reales + evento Hermes de 40 campos con T-SQL traducido a C#)— se construyó en **menos de una hora** de trabajo dirigido con Kiro. Eso fue **traspaso de código** puro: no incluyó pruebas con negocio ni integraciones a terceros reales.

**Una "hora Kiro" = una hora de una persona dirigiendo a Kiro** para producir código, pruebas y documentación. No es una HH de desarrollador escribiendo a mano; el ratio medido es mucho mayor (70 SP < 1 hora en traspaso).

**Ajuste al caso del Satélite:** E1–E3 **no son solo traspaso**. Tienen capacidad nueva que no existe en el legacy (motor de cálculo determinístico, capa configurable de reglas, repartido, DUS) y requieren **iteración con pruebas** para asegurar el determinismo del monto. Por eso NO aplico el ratio puro de traspaso (70 SP/hora); uso un ritmo más conservador: **trabajo de diseño fino + generación + revisión + pruebas por entrega**. El resultado sigue siendo órdenes de magnitud menor que un desarrollo tradicional.

**Supuestos** `[SUPUESTO]`:
- 1 persona dirigiendo a Kiro. Las horas son de esa persona.
- Las horas de E1–E3 son estimación por analogía con la medición, ajustadas por el factor "capacidad nueva + pruebas".
- Las horas de la parte de terceros (E4/E5) NO se expresan en horas Kiro porque **no las acelera Kiro**: dependen de coordinación externa (ver §4).

---

## 2. Horas Kiro por entrega — Fase A (acelerada, sin terceros)

| Entrega | Contenido | Horas Kiro (desarrollo + pruebas) |
|---------|-----------|----------------------------------:|
| **Fundaciones** | Scaffolding .NET 10 hexagonal, BD/EF Core, CI/CD | 4 – 6 |
| **E1 — Convenios automatizados** | Modelo versionado, CRUD, vigencia/alertas, extracción IA + confirmación | 8 – 12 |
| **E2 — Factura con valores ajustados** | Motor determinístico (núcleo), capa configurable, ajuste manual, OFA, repartido/DUS, pruebas por regla | 16 – 24 |
| **E3 — Detección de omisiones** | Cruce determinístico + IA propositiva | 5 – 8 |
| **Subtotal Fase A** | Valor operativo completo (prefactura valorizada para digitar) | **33 – 50 horas Kiro** |

> Interpretación: la Fase A —el valor principal del sistema— es del orden de **una semana a semana y media de trabajo dirigido con Kiro** `[SUPUESTO: jornadas de ~6 h efectivas de dirección]`, no de meses. Esto es coherente con la medición (Gate In <1 h para 70 SP de traspaso): aquí el número es mayor porque hay capacidad nueva y una batería de pruebas de cálculo, no solo traspaso.

## 3. Horas Kiro por entrega — Fase B (lado Satélite; la integración real NO la acelera Kiro)

| Entrega | Lo que SÍ acelera Kiro (lado Satélite) | Horas Kiro |
|---------|----------------------------------------|-----------:|
| **E4 — Integración automática SAP** | Puerto + adaptador + Outbox + idempotencia/reintentos, contra **stub** de SAP/Agunsa Connect | 8 – 12 |
| **E5 — Cierre del lazo y pagos** | Retorno a AEXSA, conciliación de pagos, frontend operativo (contra stubs) | 14 – 20 |
| **Subtotal Fase B (lado Satélite)** | Todo lo construible sin el tercero | **22 – 32 horas Kiro** |

> El **código del lado Satélite** de la Fase B también es rápido con Kiro (contra adaptadores simulados). Lo que **no** se mide en horas Kiro es conectar con el SAP real y Agunsa Connect y validar con negocio — eso es §4.

---

## 4. Lo que Kiro NO acelera (no se cuenta en horas Kiro — depende de terceros)

Estas piezas dependen de coordinación externa; su plazo lo marcan terceros, no el ritmo de desarrollo:

- **Proveedor de integración SAP B1** (escritura de la orden de venta + devolución del folio): AEXSA no tiene desarrollo interno con SAP (PA-001). Plazo del proveedor.
- **Habilitación de Agunsa Connect** y su gobierno de seguridad/arquitectura (PA-002). Plazo de coordinación corporativa.
- **Pruebas y validación con negocio (F05):** con los facturadores reales. Es calendario de negocio, no de código.
- **Certificación de ciberseguridad y ley de datos personales** (PA-005).
- **Migración de datos** (convenios históricos, maestro de clientes) y **puesta en producción**.

> Estos ítems se estiman en **calendario** (semanas de coordinación/espera), no en horas de desarrollo. No se promete que Kiro los acelere.

---

## 5. Resumen ejecutivo

| | Horas Kiro (desarrollo acelerado) | Depende de terceros (calendario aparte) |
|---|--:|---|
| **Fase A** (E1+E2+E3) | **33 – 50** | no |
| **Fase B lado Satélite** (E4+E5) | **22 – 32** | sí, para operar en real |
| **Total desarrollo con Kiro** | **≈ 55 – 82 horas Kiro** | — |
| Integración SAP real + pruebas negocio + ciberseguridad + producción | — | **plazo de terceros, a gestionar** |

**Lectura para presentar:**
- Todo el **desarrollo de código** del Satélite (Fases A y B, lado Satélite) es del orden de **55 a 82 horas de trabajo dirigido con Kiro** — semanas, no meses.
- El **valor operativo (Fase A)** —convenio único + factura ajustada + detección de omisiones— está en el rango de **33 a 50 horas Kiro** y **no depende de terceros**.
- Lo que marca el plazo real a producción **no es el desarrollo**, sino la **integración con SAP vía Agunsa Connect, las pruebas con negocio y la certificación de ciberseguridad** — eso se gestiona y estima aparte, en calendario, porque Kiro no lo acelera.
