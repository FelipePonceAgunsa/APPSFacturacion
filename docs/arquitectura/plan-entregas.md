# Plan de entregas por valor — Satélite de Facturación AEXSA

Versión: 1.0
Fecha: 2026-10-26
Autor: Arquitecto de Solución (`arquitecto-satelite`)
Propósito: ordenar qué se construye primero según el **valor para el negocio** y qué **no depende de terceros**, para entregar beneficio operativo antes de resolver la integración automática con SAP. Documento para presentar a negocio y base del `tasks.md`.

> Decisión de alcance confirmada (usuario, 2026-10-26): en la primera fase operativa el Satélite entrega la **prefactura ya valorizada** y el facturador la **digita en SAP como hoy**. La escritura automática de la factura en SAP llega después (Entrega 4), porque depende de terceros (proveedor + Agunsa Connect). Esto permite dar valor sin esperar ese cierre.

---

## 1. El criterio de priorización

El dolor del negocio hoy es doble y **ninguno de los dos necesita la integración automática con SAP para resolverse**:

1. **Buscar el convenio** disperso en correos, carpetas y OneDrive, sin saber cuál rige (BR-001, BR-019).
2. **Calcular el monto a mano** (tarifa, días libres, piso de dólar, recargos) → errores y alta tasa de notas de crédito (REQ-005).

Por eso se separa lo que el **desarrollo acelera y controla** (convenios, cálculo, detección de omisiones) de lo que **depende de terceros y no se acelera** (escritura en SAP vía Agunsa Connect, retorno a AEXSA, ciberseguridad). Las primeras entregas dan valor operativo real aunque la digitación en SAP siga siendo manual un tiempo.

| Lo que el desarrollo acelera y controla | Lo que depende de terceros (no se acelera) |
|---|---|
| Convenio único, versionado y vigente | Escritura de la factura en SAP (PA-001) |
| Cálculo determinístico del monto | Contrato de Agunsa Connect (PA-002) |
| Capa configurable + ajuste manual | Lectura real de tarifas desde SAP (PA-003) |
| Detección de omisiones | Retorno a AEXSA SYS para el check de salida (PA-004) |
| Prefactura valorizada para digitar | Revisión de ciberseguridad / datos personales (PA-005) |

---

## 2. Entregas (en orden de presentación)

### Entrega 1 — Convenios automatizados *(lo urgente · sin terceros)*
**Qué resuelve:** elimina la búsqueda del convenio en correos/carpetas y la duda de cuál rige.
**Qué incluye:** el convenio vive en el Satélite como fuente única; carga por lectura asistida por IA (con confirmación humana) o manual; versión y vigencia; alerta de vencimiento; multi-convenio por BL (manda el más antiguo, override registrado); complementos por correo versionados.
**Valor:** fuente única de verdad del convenio vigente desde el día uno.
**Dependencia externa:** ninguna. (REQ-004, REQ-012; ADR-007)

### Entrega 2 — Factura con valores ajustados (prefactura valorizada) *(el segundo dolor · sin terceros)*
**Qué resuelve:** elimina el cálculo manual del monto y reduce las notas de crédito.
**Qué incluye:** motor determinístico (tarifa × cantidad por unidad, USD→CLP con piso de dólar, descuento de días libres, no cobro bajo aforo, recargos, mayor m³/tonelada, DUS/BL); capa configurable para las reglas no escritas; ajuste manual auditable; armado y validación de la OFA; soporte de repartido. El facturador recibe la **OFA ya calculada** y la **digita en SAP como hoy**.
**Valor:** menos errores y menos notas de crédito de inmediato, sin esperar la integración.
**Dependencia externa:** ninguna para operar (usa un snapshot de tarifas; mientras no esté la lectura real de SAP —PA-003— se carga el catálogo por el mismo mecanismo de convenios/manual). (REQ-003, REQ-005, REQ-006, REQ-008; ADR-006, ADR-008, ADR-011)

### Entrega 3 — Detección de omisiones *(valor alto · sin terceros)*
**Qué resuelve:** la fuga de ingresos por servicios prestados que no se facturan (reefer, inspecciones, complementarios, cobro al forwarder) y los cargos de puerto tardíos.
**Qué incluye:** cruce determinístico de lo prestado contra lo valorizado, con alerta de faltantes y de necesidad de factura complementaria; la IA propone candidatos, un humano decide.
**Valor:** recupera dinero que hoy se pierde; cada decisión queda registrada.
**Dependencia externa:** ninguna (se alimenta de datos de facturación/operación, no del pago). (REQ-007; ADR-009)

### Entrega 4 — Integración automática con SAP *(aquí entran los terceros)*
**Qué resuelve:** elimina la digitación manual en SAP.
**Qué incluye:** el Satélite entrega la OFA como orden de venta a SAP vía Agunsa Connect (patrón Outbox, idempotencia, reintentos, folio-o-error); recibe el folio; lectura real de tarifas desde SAP.
**Valor:** cierra el círculo del cálculo → emisión sin digitar.
**Dependencia externa:** **bloqueada por PA-001, PA-002, PA-003** (proveedor externo + gobierno corporativo de la integración). El plazo no depende solo del equipo del Satélite. (REQ-008, REQ-009; ADR-002, ADR-003)

### Entrega 5 — Cierre del lazo y pagos *(terceros)*
**Qué resuelve:** habilita la liberación de la carga desde el resultado de facturación y refleja el estado de pago.
**Qué incluye:** retorno del folio a AEXSA SYS para el check de salida; conciliación de pagos (incluye pago agrupado multi-factura/multi-cliente); alertas de impago.
**Valor:** trazabilidad extremo a extremo operación → factura → pago.
**Dependencia externa:** **bloqueada por PA-004, PA-008** (retorno a AEXSA) y la integración de la Entrega 4. (REQ-009, REQ-010; ADR-004)

---

## 3. Cómo se presenta a negocio (resumen de una lámina)

- **Fase A — valor temprano, sin depender de terceros:** Entregas 1 + 2 + 3. El Satélite ya elimina la búsqueda del convenio, entrega la factura con los valores ajustados (que el facturador digita como hoy) y avisa lo que se está dejando de cobrar. Es lo que el desarrollo con IA acelera.
- **Fase B — eliminación de la digitación y cierre del círculo:** Entregas 4 + 5. Requiere al proveedor de la integración SAP y al gobierno de Agunsa Connect; su plazo se estima y gestiona aparte, porque no lo acelera el desarrollo.

> Mensaje clave para negocio: **el beneficio no espera a la integración.** El valor principal (convenio único + factura ajustada + no perder cobros) se entrega en la Fase A. La Fase B automatiza el último tramo (digitación) y depende de terceros.

---

## 4. Lo que hay que destrabar en paralelo (gestión, no desarrollo)
- **PA-001 / PA-002:** definir con TI/SAP/arquitectura corporativa (Iván, Francisca, Luis Veloso, Ani) la vía de escritura en SAP y el contrato de Agunsa Connect; probablemente contratar proveedor. Camino crítico de la Fase B.
- **PA-003:** confirmar con Marlene la API de lectura de tarifas.
- **PA-005:** revisión de ciberseguridad y ley de datos personales antes de producción.
- **PQ-006 (negocio):** cerrar el criterio STI vs DPW con Francisco (única regla de monto abierta).

## 5. Pendiente del entregable de arquitectura para el F02
Para presentar con el procedimiento oficial falta: estimación en **HH** por entrega, contratos de puertos campo a campo, DDL/migración, y especificación OpenAPI de la API. Se agregan como paso siguiente.
