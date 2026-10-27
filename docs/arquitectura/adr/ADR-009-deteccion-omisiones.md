# ADR-009 — Motor de detección de omisiones (IA propone, no decide)

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
Fuga de ingresos por servicios prestados que no se facturan es el núcleo del problema (REQ-007). El cruce "servicios prestados ↔ facturados" es responsabilidad de **facturación**, no de recaudación, y debe alimentarse de datos de **facturación/operación**, no del cierre de pago (RN-068, PQ-022). En el dato legacy el enlace servicio↔factura es casi inexistente (~4%, BR-013). Casos: reefer, inspecciones, servicios complementarios "anotados" que no llegan a la factura (BR-003); cargos de puerto tardíos tras facturar que requieren **factura complementaria** sin aviso automático hoy (RN-056). La IA puede proponer, nunca decidir el monto (principio).

## Decisión
Un **motor de detección de omisiones** que cruza, por operación, los **servicios prestados** contra los **ítems valorizados en la OFA**, alertando faltantes. El cruce base es **determinístico**; la IA es un complemento que **propone** candidatos difusos, siempre con confirmación humana.

1. **Cruce determinístico (base):** al armar la OFA se comparan los servicios registrados en la operación (reefer, inspecciones, complementarios, cobro al forwarder) contra los ítems valorizados; los que están prestados y no valorizados se **alertan como faltantes** (REQ-007).
2. **Candidato a IA (complemento):** detección de patrones/anomalías (p. ej. operaciones similares que sí cobraron un concepto y esta no) como **propuesta** con umbral de confianza. La IA nunca agrega el ítem ni fija su monto: propone, un humano decide (RN-070, ADR-008).
3. **Decisión registrada:** cuando se detecta un faltante, el Satélite lo **propone como ítem facturable**; incluirlo o descartarlo queda registrado (quién, cuándo, por qué) (REQ-007, RNF-01).
4. **Cargos tardíos:** si llegan cargos de puerto después de facturar, el Satélite **registra la necesidad de factura complementaria** y la alerta (RN-056, REQ-007), cerrando el hueco del "sin aviso automático".
5. **Fuente de datos:** se alimenta de facturación/operación (RN-068), no del pago; y de la conciliación del DTE (ADR-004) para detectar diferencias entre lo calculado y lo emitido.

## Alternativas consideradas
1. **Dejar que la IA decida e inserte el ítem faltante automáticamente.** Prohibido por principio: un falso positivo facturaría de más; la IA solo propone.
2. **Alimentar la detección del cierre de pago (recaudación).** Descartado: contradice RN-068; el pago llega tarde y no es responsabilidad de recaudación.
3. **Solo cruce determinístico, sin IA.** Es el mínimo viable y se entrega primero; la IA se suma como mejora incremental (no bloquea la 1ª etapa).

## Consecuencias
- Se ataca la fuga de cobro con un cruce explícito y auditable antes de emitir, más alerta de complementarias tras emitir.
- La IA mejora la cobertura sin comprometer el determinismo del monto.
- Requiere que los servicios prestados estén disponibles por operación (depende de la ingesta, ADR-005, y del dato de AEXSA, BR-013); donde el dato legacy es pobre, la detección es tan buena como su fuente (límite declarado).

## Reglas / requerimientos relacionados
REQ-007, REQ-011, RN-012, RN-056, RN-068, BR-002, BR-003, BR-013, PQ-022.
