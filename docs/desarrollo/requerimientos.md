# Requerimientos — Satélite de Facturación AEXSA (extracto para desarrollo)

Versión: 1.1 (borrador para el Arquitecto)
Fecha: 2026-10-23
Autor: Analista de Negocio (levantamiento AEXSA)
Fuentes: catálogo de reglas del levantamiento (RN-001 a RN-079) y verificaciones en el código y la base de datos de AEXSA SYS. Las reglas están en `reglas-negocio.md` de esta carpeta.
Cambios v1.1: principio "manda el negocio"; REQ-003 soporta repartido; REQ-005 soporta cobro por DUS; REQ-009 precisa que `GetFactura` es solo entrada/conciliación; retorno a AEXSA SYS; Khipu desactivado.

> ## Ajustes vigentes (2026-11-04) — leer antes que el resto
> Este documento es el extracto del `requirements.md` v1.1 para desarrollo. Después de esa versión se tomaron decisiones que **prevalecen** sobre el texto de abajo:
>
> 1. **Convenios (REQ-004, REQ-012):** se ingresan por **formulario manual** (cliente y RUT, comercial, vigencia, moneda, tipo de cambio mínimo, días libres y tabla de servicios). La lectura asistida por IA es una **etapa opcional** que se construye solo si el negocio la pide (ADR-007, ADR-012).
> 2. **Fases:** la **Fase A** entrega la prefactura valorizada que el facturador digita en SAP como hoy. La **Fase B** escribe en SAP, recibe el folio, concilia pagos y retorna a AEXSA SYS (REQ-008 a REQ-010).
> 3. **Catálogo de SAP:** en la Fase A entra por archivo Excel o CSV (ADR-017). La API de lectura (PQ-025) es de la Fase B.
> 4. **STI y DP World (PQ-006):** cerrada. Es una regla fija por puerto (ADR-015, RN-078, RN-079). Ya no es un riesgo abierto.
> 5. **Autenticación:** Cognito federado con Entra ID (ADR-016). No se manejan contraseñas propias.
> 6. **Detección de omisiones (REQ-007):** el cruce determinístico es el núcleo. La propuesta por IA es etapa opcional.
> 7. **Pantallas:** sin tarjetas de indicadores (KPI) en la primera versión. Lo simple primero.
>
> Este documento define el **QUÉ** (requerimientos de negocio). El **CÓMO** (arquitectura, tecnología, modelo de datos) lo define el Arquitecto. Cada requerimiento referencia las reglas de negocio (RN-XXX) que lo originan. Los criterios de aceptación usan formato EARS (WHEN/IF/WHILE … THEN el Satélite SHALL …).

---

## 1. Introducción

### 1.1 Problema
La facturación de AEXSA depende de digitación manual en SAP Business One por escritorio remoto. El sistema operacional (AEXSA SYS) no alimenta SAP: qué y cuánto cobrar se decide caso a caso con convenios dispersos (SharePoint, correos, carpetas) y con reglas que viven en la memoria de los facturadores. No hay cruce sistemático entre servicios prestados, facturas emitidas y pagos, lo que provoca riesgo de omisión de cobro y una alta tasa de notas de crédito.

### 1.2 Objetivo
Construir el Satélite de Facturación: un componente que, integrado a AEXSA SYS y SAP B1, centraliza los eventos facturables, calcula automáticamente convenios y tarifas de forma determinística y auditable, arma una Orden de Facturación (OFA) valorizada, y da trazabilidad de punta a punta (operación → factura → pago) para detectar omisiones.

### 1.3 Alcance de la 1ª etapa
- **Dentro:** contenedor FULL con y sin convenio (≈90–95% del negocio, RN-041); los 7 pasos del ciclo; la OFA hacia SAP; la trazabilidad y detección de omisiones.
- **Fuera (2ª etapa):** carga suelta / LCL con clasificación por letras A–F (negocio confidencial, fuera de sistema, RN-040, BR-018); licitaciones grandes y su análisis de negociación (ingreso manual); gestión contable de notas de crédito y devoluciones (se registran, no se calculan).

### 1.4 Principios no negociables
- **Manda el negocio, no el sistema actual.** El Satélite es un sistema nuevo. Cuando lo que hace AEXSA SYS hoy difiere de lo que el negocio necesita, prevalece la necesidad del negocio. Las verificaciones en código/BD indican "qué existe hoy" y "qué falta construir", no un límite al diseño. (criterio del usuario, 2026-10-23)
- **Los montos se calculan con lógica determinística y auditable; la IA nunca fija un monto** (solo propone mapeos o alerta diferencias, con confirmación humana).
- **El Satélite no exige tener todas las reglas de monto automatizadas** (muchas viven en la memoria de los facturadores y no se pueden levantar): debe ser configurable/manejable y permitir intervención manual auditable (RN-070).
- Toda decisión y ajuste quedan trazables (quién, cuándo, por qué).

### 1.5 Supuestos y riesgos abiertos (no bloquean el diseño, pero deben considerarse)
- **PQ-029 / BR-027 / RN-076:** la escritura de la factura/OFA en SAP y la devolución del folio no está resuelta; el único canal que existe (`GetFactura`) es de entrada/conciliación. Probable proveedor externo.
- **PQ-030 / BR-028:** la integración debe pasar por el middleware corporativo **Agunsa Connect** y su gobierno de seguridad/arquitectura.
- **PQ-025:** la lectura de artículos y lista de precios desde SAP es viable por API de consulta; falta construirla y confirmar el objeto exacto.
- **PQ-006 / BR-007:** criterio STI vs DPW (refacturación en cero) pendiente de confirmar con negocio.
- **Capacidades nuevas que el legacy no tiene (manda el negocio):** repartir la factura entre varios destinatarios (RN-077/BR-024) y cobro por DUS en expo (RN-047/BR-030); ambas requieren desarrollo nuevo.

---

## 2. Requerimientos funcionales

### REQ-001 — Recepción del evento facturable (Paso 1)
**Historia:** Como analista de facturación, quiero que el Satélite reciba automáticamente desde AEXSA SYS el evento facturable con sus atributos, para no depender de que alguien identifique manualmente qué hay que cobrar.
Reglas: RN-001, RN-002, RN-026, RN-027, RN-028, RN-046.
Criterios (EARS):
- WHEN el visador aprueba el visado de una operación en AEXSA SYS THEN el Satélite SHALL recibir el evento facturable con sus atributos (tipo de carga, tipo de retiro, cliente/consignatario, puerto, servicios asociados, DIN/DUS).
- IF la operación tiene convenio THEN el Satélite SHALL tratar el evento como facturación posterior al despacho (tope 7 días desde el último despacho).
- IF la operación es sin convenio o carga suelta THEN el Satélite SHALL tratar el evento como facturación en la visación.
- WHEN se recibe un evento THEN el Satélite SHALL contrastar DIN contra papeleta y, IF hay discrepancia, SHALL marcar la operación como no facturable y devolverla a corrección.

### REQ-002 — Clasificación de la ruta de facturación (Paso 1)
**Historia:** Como analista de facturación, quiero que el Satélite clasifique automáticamente la ruta de la operación, para aplicar el tratamiento correcto sin decisión manual.
Reglas: RN-003, RN-007, RN-008, RN-044, RN-026, RN-027.
Criterios:
- WHEN se recibe un evento facturable THEN el Satélite SHALL clasificar la ruta por {tipo de carga} × {tipo de retiro: directo/indirecto} × {con/sin convenio} (≈8–9 combinaciones reales).
- IF un contenedor inscrito como directo es reclasificado a indirecto por inspección de aduana THEN el Satélite SHALL recalcular la ruta y la tarifa aplicable.
- IF el consignatario tiene convenio vigente THEN el Satélite SHALL aplicar el convenio con precedencia sobre el kit de inspección (RN-045).

### REQ-003 — Determinación del destinatario de la factura (Paso 1)
**Historia:** Como analista de facturación, quiero que el Satélite determine a quién se factura (incluido repartir entre varios), para respetar las instrucciones comerciales y evitar errores de consignatario.
Reglas: RN-002, RN-038, RN-039, RN-050, RN-057, RN-077.
Nota (capacidad nueva): AEXSA SYS hoy soporta **un solo facturador** por operación y **no permite repartido** (RN-077, verificado en BD). Dado que el negocio sí reparte (ej. un cliente: parte al consignatario, parte al forwarder, RN-050), el Satélite **debe soportar repartido** como capacidad nueva (manda el negocio).
Criterios:
- WHEN se arma la facturación THEN el Satélite SHALL usar por defecto como destinatario al consignatario indicado en la DIN.
- IF existe instrucción comercial de facturar al forwarder/embarcador THEN el Satélite SHALL aplicar esa instrucción y registrar su origen.
- IF la instrucción comercial indica **repartir** el cobro entre consignatario y forwarder THEN el Satélite SHALL permitir asignar conceptos/montos a más de un destinatario en la misma operación.
- WHEN se emite el documento THEN el Satélite SHALL usar el RUT de la declaración (DIN/DUS), no el de la papeleta.

### REQ-004 — Gestión del convenio vigente (Paso 2)
**Historia:** Como analista de facturación, quiero que el Satélite identifique el convenio vigente del cliente y sus condiciones desde una fuente estructurada, para no buscar en correos y carpetas.
Reglas: RN-005, RN-015, RN-018, RN-021, RN-022, RN-023, RN-037, RN-043, RN-041.
Criterios:
- WHEN una operación requiere convenio THEN el Satélite SHALL ubicar el convenio vigente del cliente con sus condiciones (moneda, tipo de cambio mínimo, días libres, vigencia) y la lista de servicios con tarifa y unidad.
- IF existe más de un convenio aplicable al mismo BL THEN el Satélite SHALL proponer el más antiguo y SHALL permitir override comercial registrado.
- IF el convenio superó su vigencia (1 año desde la aceptación) THEN el Satélite SHALL alertar el vencimiento.
- WHEN un convenio se ajusta por correo THEN el Satélite SHALL permitir registrar ese ajuste como complemento del convenio, versionado.

### REQ-005 — Cálculo determinístico de tarifas (Paso 3)
**Historia:** Como analista de facturación, quiero que el Satélite calcule los montos automáticamente de forma auditable, para eliminar el cálculo manual y reducir errores y notas de crédito.
Reglas: RN-014, RN-022, RN-023, RN-024, RN-025, RN-032, RN-033, RN-045, RN-047, RN-048, RN-051, RN-052, RN-053, RN-054.
Criterios:
- WHEN se valoriza una operación THEN el Satélite SHALL calcular, por cada servicio prestado, cantidad × tarifa con la unidad correcta, de forma determinística y reproducible.
- IF un ítem no tiene tarifa de convenio THEN el Satélite SHALL aplicar la tarifa pública (kit spot de SAP) y SHALL marcarlo como excepción.
- WHEN la tarifa está en USD THEN el Satélite SHALL convertir a CLP con el dólar del día y SHALL nunca aplicar un valor bajo el tipo de cambio mínimo del convenio.
- WHEN se calcula almacenaje THEN el Satélite SHALL descontar los días libres del convenio, y IF hay aforo SHALL no cobrar almacenaje mientras dure esa condición.
- IF corresponde un recargo (15% refacturación, 90% falso flete, sobrepeso, habilitación fuera de horario) THEN el Satélite SHALL aplicarlo según regla.
- IF la operación es exportación THEN el Satélite SHALL cobrar **por cantidad de DUS** (RN-047); IF es importación SHALL cobrar por BL/tonelada/m³ según el concepto, aplicando el mayor entre m³ y tonelada cuando corresponda. (Capacidad nueva: AEXSA SYS no modela el DUS como driver de cobro —BR-030, verificado en BD—; el Satélite debe incorporarlo porque manda el negocio.)
- WHEN entrega el resultado THEN el Satélite SHALL registrar qué tarifa, qué versión de convenio y qué tipo de cambio se usaron.

### REQ-006 — Capa configurable para reglas no automatizadas (Paso 3, transversal)
**Historia:** Como analista de facturación, quiero poder ajustar o agregar manualmente conceptos y montos cuando una regla no está automatizada, para no bloquear la facturación por las reglas que hoy viven en la memoria del equipo.
Reglas: RN-070, RN-053, BR-023, PQ-026.
Criterios:
- WHEN una regla de monto no está automatizada THEN el Satélite SHALL permitir al facturador agregar/ajustar el concepto, monto o excepción de forma manual-asistida.
- WHEN el facturador realiza un ajuste manual THEN el Satélite SHALL registrarlo de forma auditable (usuario, fecha, motivo, valor anterior y nuevo).
- WHILE una regla manual se repite THEN el Satélite SHALL permitir promoverla a configuración reutilizable.
- IF se aplica un ajuste manual THEN el monto final SHALL seguir siendo determinístico y auditable (la IA no fija el valor).

### REQ-007 — Detección de omisiones de cobro (Paso 3 y 7, transversal)
**Historia:** Como jefe de facturación, quiero que el Satélite detecte servicios prestados que no fueron facturados, para evitar la pérdida de ingresos que hoy ocurre.
Reglas: RN-012, RN-056, RN-068, BR-002, BR-003, BR-013.
Criterios:
- WHEN se arma la OFA THEN el Satélite SHALL cruzar los servicios prestados en la operación contra los ítems valorizados y SHALL alertar los faltantes (reefer, inspecciones, servicios complementarios, cobro al forwarder).
- IF llegan cargos de puerto tardíos tras facturar THEN el Satélite SHALL registrar la necesidad de una factura complementaria y SHALL alertarla (hoy no hay aviso automático).
- WHEN detecta un faltante THEN el Satélite SHALL proponerlo como ítem facturable; la decisión de incluirlo SHALL quedar registrada.

### REQ-008 — Emisión de la OFA hacia SAP (Paso 4)
**Historia:** Como analista de facturación, quiero que el Satélite entregue a SAP una Orden de Facturación valorizada y validada, para que SAP solo emita sin digitación manual.
Reglas: RN-059, RN-060, RN-025, RN-050, RN-057.
Criterios:
- WHEN el cálculo está completo THEN el Satélite SHALL armar la OFA (equivalente a la orden de venta de SAP) con ítems, cantidades, unidades, tarifas, moneda, montos, destinatario y RUT de la DIN.
- WHEN la OFA está lista THEN el Satélite SHALL validar su completitud (destinatario definido, todos los ítems con tarifa o marcados, recargos y días resueltos) antes de entregarla.
- IF la OFA está incompleta THEN el Satélite SHALL bloquear la entrega y SHALL indicar qué falta.
- WHEN entrega la OFA THEN el Satélite SHALL hacerlo a través de la integración con SAP (vía a definir por el Arquitecto, pasando por Agunsa Connect).

### REQ-009 — Emisión de la factura y retorno de folio (Paso 5)
**Historia:** Como analista de facturación, quiero que SAP emita el DTE a partir de la OFA y devuelva el folio al Satélite, para mantener la trazabilidad factura↔operación.
Reglas: RN-059, RN-029, RN-034, RN-062, RN-072, RN-074, RN-076.
Nota técnica (verificado en código): el canal existente `API/SAP/GetFactura` es solo de **entrada** (SAP→AEXSA): trae el DTE XML con folio y montos para **conciliar**. NO existe canal de **salida** (crear la OFA/orden de venta en SAP) → esa pieza es desarrollo nuevo (RN-076, BR-027/PQ-029).
Criterios:
- WHEN SAP recibe la OFA THEN SAP SHALL emitir el DTE asignando folio, y el Satélite SHALL recibir el folio y número de factura interna (vía a construir; hoy solo existe el canal de conciliación de entrada).
- WHEN el Satélite recibe el folio THEN el Satélite SHALL cerrar el vínculo operación↔factura por folio.
- WHEN la factura está emitida THEN el Satélite SHALL **devolver el resultado a AEXSA SYS** (documento/folio) para habilitar el check de salida que libera la carga (RN-072). Los datos exactos a retornar están por confirmar (PQ-032).
- IF la emisión falla o el folio no retorna en un plazo THEN el Satélite SHALL marcar la OFA como "emitida sin confirmación" y SHALL alertarla.
- WHEN se emite una nota de crédito o guía (DTE 61/112/52) THEN el Satélite SHALL registrarla sin calcular su gestión contable (fuera de alcance).

### REQ-010 — Confirmación y conciliación del pago (Paso 6)
**Historia:** Como analista de recaudación, quiero que el Satélite refleje el estado de pago conciliado en SAP, para saber qué está pagado sin revisar manualmente.
Reglas: RN-013, RN-063, RN-064, RN-065, RN-066, RN-067, RN-069, RN-029, RN-030.
Criterios:
- WHEN SAP registra un Pago Recibido (PR) que reconcilia una factura THEN el Satélite SHALL recibir el estado de pago (pagada/pendiente/parcial) asociado al folio.
- WHEN una factura con crédito supera su vencimiento sin pago THEN el Satélite SHALL alertar el impago.
- IF un pago agrupa varias facturas (o de varios clientes) THEN el Satélite SHALL soportar la asociación del pago a múltiples folios.
- WHILE una factura está impaga el Satélite SHALL mantener su estado visible para cobranza.
- WHEN se registra una nota de crédito / saldo a favor THEN el Satélite SHALL reflejar el saldo y marcar la necesidad de devolución a quien efectuó el pago.

### REQ-011 — Registro histórico y auditoría (Paso 7)
**Historia:** Como jefe de facturación/gestión, quiero un registro histórico completo y auditable de cada operación facturada, para explicar cada monto y medir el proceso.
Reglas: RN-029, RN-013, RN-043, RN-056, RN-070.
Criterios:
- WHEN una operación se factura THEN el Satélite SHALL registrar un histórico con: ítems y montos, convenio/tarifa y su versión, destinatario y RUT, folio y tipo de DTE, estado de pago, y excepciones/ajustes aplicados.
- WHEN se aplica una tarifa THEN el Satélite SHALL fijar la versión de tarifa usada por fecha del evento, para poder reconstruir el "por qué" del monto.
- WHEN existe factura complementaria o nota de crédito THEN el Satélite SHALL asociarla a la misma operación.
- WHEN se consulta una operación cerrada THEN el Satélite SHALL permitir reconstruir el cálculo completo de forma auditable.

### REQ-012 — El convenio vive en el Satélite (soporte al Paso 2)
**Historia:** Como ejecutivo comercial, quiero generar e ingresar el convenio en el Satélite, para que sea la fuente única y vigente que usa la facturación, en vez de buscarlo en SharePoint o correos.
Reglas: RN-071, RN-015, RN-035, RN-037, RN-043, BR-001, BR-019.
Decisión de alcance (2026-10-22): el Satélite es la **fuente autoritativa** del convenio vigente. El comercial lo negocia y lo ingresa; el Satélite lo estructura, versiona y lo pone disponible para el cálculo.
Criterios:
- WHEN el comercial ingresa un convenio (PDF/Word) THEN el Satélite SHALL permitir su carga por **lectura asistida por IA** (extrae servicios, tarifas, unidades, moneda, TC mínimo, días libres y vigencia) **o por ingreso manual**, con confirmación humana antes de quedar vigente.
- WHEN un convenio queda vigente THEN el Satélite SHALL registrarlo con versión y fechas de vigencia, y SHALL ser la fuente que consulta el Paso 2/3 (no SharePoint ni correos).
- WHEN existe más de una versión de un convenio THEN el Satélite SHALL identificar la vigente y conservar el histórico.
- IF un ajuste llega por correo THEN el Satélite SHALL permitir registrarlo como complemento versionado del convenio.

---

## 3. Requerimientos no funcionales

- **RNF-01 Auditabilidad:** todo cálculo, ajuste manual y decisión debe ser reconstruible (quién, cuándo, qué versión de tarifa/convenio, qué tipo de cambio). Base del valor del sistema. (RN-025, RN-043, RN-070)
- **RNF-02 Trazabilidad extremo a extremo:** operación → OFA → factura (folio) → pago, sin perder documentos en el camino. (RN-029, BR-013)
- **RNF-03 Determinismo del monto:** el cálculo de montos no usa IA; es reproducible y explicable. (principio del proyecto)
- **RNF-04 Volúmenes:** dimensionar para ~2.428 documentos de venta/mes y ~1.468 pagos/mes (2025), con picos; un pago puede agrupar varias facturas. (PQ-011, RN-067)
- **RNF-05 Integración gobernada:** la comunicación con SAP debe pasar por Agunsa Connect y el gobierno de seguridad/arquitectura del grupo. (PQ-030, BR-028)
- **RNF-06 No disrupción:** el Satélite complementa el flujo actual (visado, SAP, facturación electrónica) sin reemplazar la emisión del DTE, que sigue en SAP. (RN-059, RN-062)
- **RNF-07 Resiliencia de integración:** toda OFA entregada debe terminar con folio o con error explícito; reintentos y control de estado. (PQ-029, BR-027)

---

## 4. Necesidades para el Arquitecto (restricciones y decisiones a diseñar)

1. **Integración con SAP B1** de lectura (artículos, lista de precios — API de consulta viable, PQ-025) y de escritura (crear la OFA/orden de venta y recibir folio — no resuelta, BR-027/PQ-029), pasando por **Agunsa Connect** (PQ-030/BR-028). Probable proveedor externo.
2. **Motor de tarifas determinístico y versionado** (tarifa por servicio/cliente con vigencia; cantidad × tarifa; conversión USD→CLP con piso; días libres; aforo; recargos; factor mayor; DUS/BL).
3. **Capa configurable para reglas no escritas (RN-070)** como componente central, con auditoría y promoción de reglas recurrentes a configuración.
4. **Fuente estructurada de convenios** con histórico de versiones y extracción asistida por IA desde .docx/.pdf/.msg (BR-001).
5. **Motor de detección de omisiones** (cruce prestado vs. facturado), alimentado de datos de facturación/operación, no del cierre de pago (RN-068). Candidato a IA (propone; no decide montos).
6. **Repositorio histórico auditable** (base de trazabilidad e indicadores/KPIs futuros).
7. **Identificación de cliente por RUT** y mapeo con la ficha de SAP (condición de pago, lista de precios) (BR-020, RN-065).
8. **Catálogo normalizado de servicios/unidades** a partir de `MasterData.TipoServicio` (192 servicios, BR-014).
9. **Soporte de facturación repartida** (varios destinatarios por operación): capacidad que AEXSA no tiene (un solo facturador, RN-077); el negocio la necesita (RN-050). Modelar múltiples destinatarios con sus conceptos/montos.
10. **DUS como driver de cobro en exportación** (RN-047): AEXSA no lo modela (BR-030); incorporar cantidad de DUS por operación como unidad de cobro. Priorizar según si expo/LCL entra en 1ª etapa.
11. **Retorno a AEXSA SYS** del resultado de facturación para habilitar el check de salida (RN-072/RN-074); existe el canal de entrada `GetFactura` (conciliación), evaluar reutilizarlo.
12. **Nota:** la pasarela Khipu está en el código pero **desactivada** (RN-075); no diseñar el Paso 6 sobre ella salvo que el negocio la reactive.

---

## 5. Trazabilidad requerimiento → reglas (resumen)

| REQ | Paso | Reglas principales |
|-----|------|--------------------|
| REQ-001 | 1 | RN-001, 002, 026, 027, 028, 046 |
| REQ-002 | 1 | RN-003, 007, 008, 044, 045 |
| REQ-003 | 1 | RN-002, 038, 039, 050, 057, 077 |
| REQ-004 | 2 | RN-005, 015, 018, 021, 022, 023, 037, 043 |
| REQ-005 | 3 | RN-014, 022, 023, 024, 025, 032, 033, 045, 047, 048, 051, 052, 053, 054 |
| REQ-006 | 3/transv. | RN-070, 053 |
| REQ-007 | 3/7 | RN-012, 056, 068 |
| REQ-008 | 4 | RN-059, 060, 025, 050, 057 |
| REQ-009 | 5 | RN-059, 029, 034, 062, 072, 074, 076 |
| REQ-010 | 6 | RN-013, 063, 064, 065, 066, 067, 069, 029, 030 |
| REQ-011 | 7 | RN-029, 013, 043, 056, 070 |
| REQ-012 | 2 | RN-071, 015, 035, 037, 043 |

---

## 6. Preguntas críticas abiertas (riesgos para el diseño)
Resueltas tras verificación en código/BD (2026-10-23): PQ-002 (convenio vive en el Satélite, RN-071), PQ-032 (retorno a AEXSA SYS existe vía GetFactura, RN-074), PQ-023 (destinatario = ClienteFacturadorID, RN-077), PQ-027 (AEXSA no cobra por DUS → capacidad nueva, BR-030).

Siguen abiertas:
- **PQ-029 / BR-027 / RN-076 (técnica, la crítica):** no existe canal de escritura hacia SAP; crear la OFA/orden de venta y recibir folio es desarrollo nuevo (probable proveedor).
- **PQ-030 / BR-028:** restricciones de Agunsa Connect y gobierno de la integración.
- **PQ-025:** construir la API de lectura de SAP y confirmar el objeto/query exacto.
- **PQ-006 / BR-007 (negocio):** criterio STI vs DPW (poner refacturación en cero). Única regla de monto de negocio sin confirmar; pendiente con jefatura de facturación.
- **BR-018:** carga LCL/expo por DUS fuera de alcance de 1ª etapa (confirmar límite con negocio).
