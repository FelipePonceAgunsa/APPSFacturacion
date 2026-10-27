# ADR-013 — Carga suelta y letra tarifaria (diseño anticipado, 2ª etapa)

Estado: **Aceptado como diseño anticipado** · Construcción diferida a **2ª etapa**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

> Propósito del ADR: la carga suelta / letra tarifaria se construye en 2ª etapa (decisión de negocio, BR-018). Se diseña **ahora** para (1) tener claro cómo se hará y (2) asegurar que el diseño de 1ª etapa **no se cierre** a esta extensión. Cambiar esto en el diseño es barato; cambiarlo en código construido, no.

## Contexto
En carga LCL/suelta un contenedor trae ~10 clientes; **cada BL es un cliente** y se clasifica por **letra A–F** (RN-040). Cada letra apunta a un **kit de cobros** (grupo de servicios + tarifas). La clasificación la define el MBO/CC y llega **por correo (tabla/Excel)**, cambia a diario y se asigna a mano BL por BL (PQ-007, RN-040). La carga suelta **no tiene lista de precios en SAP** (BR-025): el valor vive en una carpeta de cotizaciones y se multiplica a mano por el factor (m³/ton/BL/DUS, RN-048). Es confidencial (rebate) y de alto volumen. Por eso el negocio la dejó en 2ª etapa (BR-018); la 1ª etapa es FULL con convenio (90–95%, RN-041).

## Decisión (diseño objetivo para 2ª etapa)
Modelar la letra tarifaria como una **clasificación por BL que mapea a un kit de cobros**, reutilizando el motor de tarifas y el modelo de datos de la 1ª etapa, con tres piezas nuevas:

1. **Entidad `LetraTarifaria` (catálogo A–F):** cada letra define un **conjunto de servicios con su tarifa/unidad** (el "kit de cobros"). Es, conceptualmente, lo mismo que un convenio pero indexado por letra en vez de por cliente. Versionada con vigencia (cambia a diario → cada versión data).
2. **Asignación `BL → letra`:** la operación de carga suelta se descompone en **múltiples BL**, y a cada BL se le asigna una letra. Esta asignación es la que hoy llega por correo/Excel.
3. **Captura de la asignación:** carga del correo/Excel del MBO/CC mediante **lectura asistida por IA + confirmación humana** (mismo patrón que convenios, ADR-007/012): la IA propone la tabla BL↔letra extraída del Excel/correo; una persona confirma. Nunca la IA fija el cobro.
4. **Fuente de tarifas de carga suelta:** como no está en SAP (BR-025), la **cotización de carga suelta** se estructura en el Satélite como fuente de tarifas (análoga a `servicio_convenio`), por letra. Es un trabajo de 2ª etapa (hoy no existe el dato estructurado).
5. **Cálculo:** cada BL se valoriza con el **mismo motor de tarifas** (pipeline de ADR-006): la letra aporta la tarifa (en lugar del convenio), y aplican las mismas reglas de cantidad/unidad (incluye "mayor m³/ton" RN-048 y DUS en expo RN-047), moneda y recargos. No es un motor nuevo.
6. **Facturación:** cada BL puede producir su propia línea/OFA; la operación de carga suelta agrupa varios BL (y por tanto varios clientes/destinatarios), reutilizando el **repartido** ya diseñado (ADR-011).
7. **Confidencialidad (rebate):** los datos de rebate se tratan como sensibles (control de acceso por rol, cifrado), igual que los datos personales (BR-021).

## Qué del diseño de 1ª etapa YA lo soporta (validación de no bloqueo)
- **Motor de tarifas (ADR-006):** la fuente de la tarifa es un parámetro del `ContextoCalculo` (convenio **o** tarifa pública). Agregar "tarifa por letra" es una **tercera fuente**, no un motor nuevo. ✔ No bloquea.
- **Unidad de cobro (ADR-011):** `UnidadCobro` ya incluye BL/DUS/M3/TON y el "mayor paga". La carga suelta usa las mismas. ✔ No bloquea.
- **Repartido (ADR-011):** varios destinatarios por operación ya está modelado; carga suelta = muchos BL/clientes en un contenedor. ✔ No bloquea.
- **Extracción asistida por IA (ADR-007/012):** el patrón "documento → propuesta → confirmación humana" sirve igual para el Excel/correo de letras. ✔ No bloquea.
- **Modelo de datos (ADR-010):** `tipo_servicio`, `linea_ofa`, `asignacion_cobro`, `memoria_calculo` se reutilizan. Faltaría agregar `letra_tarifaria`, `servicio_letra` y un vínculo `bl → letra` en la operación. Son tablas **aditivas**, no cambian las existentes. ✔ No bloquea.

## Ajuste preventivo en el diseño de 1ª etapa (barato ahora)
Para no cerrar la puerta, se deja previsto (sin construir):
- En `operacion`, contemplar que una operación de carga suelta se **descompone en BL** (hoy la 1ª etapa es 1 operación = 1 facturación FULL; el modelo admite agregar una entidad `bl` asociada a la operación en 2ª etapa sin romper lo existente).
- En el motor, la "fuente de tarifa" se diseña como **estrategia enchufable** (convenio | pública | letra), no como un `if` cerrado a dos casos. Esto ya está implícito en la Regla 1 del `motor-tarifas-detalle.md`; se explicita aquí.

## Alternativas consideradas
1. **Construir carga suelta en 1ª etapa.** Descartada por el negocio (BR-018): arrancar por lo más complejo, sin fuente de datos (BR-025) y de menor proporción (RN-041) retrasaría el 90–95% que sí se puede resolver ya.
2. **Motor de tarifas separado para carga suelta.** Descartada: duplicaría lógica (moneda, mayor m³/ton, recargos). Se reutiliza el mismo motor con la letra como fuente de tarifa.
3. **Letra como simple texto en la línea.** Descartada: la letra debe apuntar a un kit versionado de servicios/tarifas, o no se puede calcular ni auditar.

## Consecuencias
- La 1ª etapa queda **validada como no bloqueante**: la carga suelta se suma por extensión (tablas aditivas + tercera fuente de tarifa), sin reescribir el motor ni el modelo.
- Se identifica el trabajo real de 2ª etapa: **estructurar la fuente de tarifas de carga suelta** (hoy inexistente, BR-025) y la **captura de la tabla BL↔letra** del MBO/CC.
- Riesgo trasladado a 2ª etapa: confidencialidad del rebate y alto volumen/variabilidad diaria; se abordan con control de acceso y con la captura asistida.

## Reglas / requerimientos relacionados
RN-040, RN-047, RN-048, BR-018, BR-025, PQ-007. Reutiliza ADR-006, ADR-007, ADR-010, ADR-011, ADR-012. Fuera de alcance de la 1ª etapa (REQ §1.3).
