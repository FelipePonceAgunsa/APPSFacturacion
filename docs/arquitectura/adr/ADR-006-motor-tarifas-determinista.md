# ADR-006 — Motor de tarifas determinístico y versionado

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
El corazón del Satélite es calcular, por cada servicio prestado, el monto de forma **determinística, reproducible y auditable** (REQ-005, RNF-03). La IA **nunca fija un monto** (principio del proyecto). El cálculo combina múltiples reglas: cantidad × tarifa con la unidad correcta; tarifa de convenio vs. tarifa pública (kit spot, RN-014) con marca de excepción; conversión USD→CLP con el dólar del día y **piso de tipo de cambio** del convenio (RN-022); descuento de **días libres** y no cobro de almacenaje bajo aforo (RN-023, RN-051, RN-052); recargos (15% refacturación, 90% falso flete, sobrepeso, fuera de horario — RN-024); "el mayor paga" entre m³ y tonelada (RN-048); cobro por **DUS** en expo y por BL/ton/m³ en impo (RN-047, ver ADR-011). Cada cálculo debe guardar qué tarifa, qué versión de convenio y qué tipo de cambio usó (REQ-005, REQ-011).

## Decisión
Implementar un **motor de reglas determinístico como una canalización (pipeline) de reglas puras y versionadas**, en el dominio, sin dependencias de infraestructura ni de IA, totalmente cubierto por pruebas.

1. **Pipeline de cálculo:** la valorización de una operación pasa por pasos ordenados y explícitos, cada uno una **regla pura** `(ContextoCalculo) → AjusteLinea[]`:
   - Resolución de tarifa por ítem (convenio → si falta, tarifa pública/kit spot con marca de excepción, RN-006/RN-014).
   - Cálculo de cantidad por unidad (incluye "el mayor entre m³ y tonelada", RN-048; DUS/BL, RN-047).
   - Conversión de moneda USD→CLP con piso de TC del convenio (RN-022).
   - Almacenaje: días calculados menos días libres, no cobro bajo aforo (RN-023/RN-051/RN-052).
   - Recargos aplicables (RN-024 y otros, cuando estén configurados).
2. **Determinismo:** las reglas son funciones puras sobre un `ContextoCalculo` inmutable (datos de la operación + versión de convenio + versión de snapshot de tarifa + TC del día). Dado el mismo contexto, el resultado es idéntico. Sin estado oculto, sin IA, sin aleatoriedad.
3. **Versionado:** cada regla tiene versión; cada cálculo persiste un **`MemoriaCalculo`** con las reglas y versiones aplicadas, la versión de convenio (ADR-007), la versión de snapshot de tarifa (ADR-003) y el TC usado. Reconstruir el "por qué" de un monto = releer la `MemoriaCalculo` (REQ-011, RNF-01).
4. **Reglas no automatizadas:** las que no están en el pipeline se cubren con la capa configurable + ajuste manual auditable (ADR-008); el resultado sigue siendo determinístico.
5. **Pruebas:** cada regla y el pipeline completo tienen pruebas unitarias con casos del levantamiento (días libres, aforo, piso de TC, recargos, mayor m³/ton). Es requisito, no opcional (principio: "con pruebas").

## Alternativas consideradas
1. **Motor de reglas genérico/externo (p. ej. un rules engine con DSL).** Descartado para la 1ª etapa: añade una capa de indirección y un lenguaje que dificulta la auditoría línea a línea; las reglas de AEXSA son numerosas pero expresables en código puro probado. Se puede reconsiderar si la capa configurable (ADR-008) crece mucho.
2. **Calcular en SAP y que el Satélite solo arme la orden.** Descartado: el Satélite debe detectar omisiones (REQ-007) y auditar el cálculo (REQ-011); necesita el cálculo propio.
3. **Permitir que la IA ajuste montos cuando falta una regla.** Prohibido por principio (RNF-03). La IA solo propone/alerta (ADR-009); el monto lo fija una regla o un humano con registro.

## Consecuencias
- El cálculo se construye y prueba **antes** de resolver la integración SAP (desacople de riesgo, alineado con ADR-001/ADR-002).
- La auditabilidad es estructural: la `MemoriaCalculo` permite reconstruir cualquier monto histórico aunque cambien tarifas o convenios después.
- Costo: disciplina de mantener reglas puras y versionadas y una batería de pruebas que crece con cada regla incorporada.

## Reglas / requerimientos relacionados
REQ-005, REQ-006, REQ-011, RNF-01, RNF-03, RN-014, RN-022, RN-023, RN-024, RN-045, RN-047, RN-048, RN-051, RN-052, RN-053, RN-054.
