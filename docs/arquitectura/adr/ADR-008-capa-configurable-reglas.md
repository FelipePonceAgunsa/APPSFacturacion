# ADR-008 — Capa configurable para reglas no escritas

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
Requisito central, no accesorio (handoff §2.3, RN-070, REQ-006): ~40 reglas que determinan el monto **no están escritas** y viven en la memoria de los facturadores; no se levantarán todas (BR-023, PQ-026). El Satélite no exige tenerlas automatizadas: debe permitir al facturador **intervenir, ajustar o agregar** conceptos/montos/excepciones de forma manual-asistida, con registro auditable, y permitir **promover** reglas recurrentes a configuración. El monto final sigue siendo determinístico y auditable; la IA puede proponer, nunca fijar (RN-070, REQ-006).

## Decisión
Incorporar una **capa configurable con tres niveles** que convive con el motor determinístico (ADR-006): (1) reglas en código, (2) reglas en configuración de datos, (3) ajuste manual auditable; con un camino explícito de **promoción** de (3) → (2).

1. **Nivel 1 — reglas en código** (ADR-006): las reglas conocidas y estables (días libres, piso TC, recargos, mayor m³/ton).
2. **Nivel 2 — reglas en datos (`ReglaConfigurable`):** reglas parametrizables que un usuario autorizado crea/edita sin desplegar código: condición (p. ej. forwarder X, puerto Y, concepto Z) → acción (agregar concepto, poner en cero, aplicar factor). Versionadas con vigencia. El pipeline del motor las evalúa como un paso más, de forma determinística.
3. **Nivel 3 — ajuste manual asistido (`AjusteManual`):** cuando ninguna regla cubre el caso, el facturador agrega/ajusta el concepto o monto en la OFA. Se registra **usuario, fecha, motivo, valor anterior y nuevo** (REQ-006, RNF-01). El monto lo fija la persona (determinístico y trazable), nunca la IA.
4. **Promoción:** cuando un mismo `AjusteManual` se repite (el sistema lo detecta y lo sugiere), un usuario autorizado puede **promoverlo a `ReglaConfigurable`** (Nivel 2). Así el sistema aprende sin que la IA fije montos.
5. **La IA solo propone:** puede sugerir un ajuste o una promoción (con umbral de confianza), que un humano acepta; nunca aplica el monto por sí misma (ADR-009, principio).
6. **Auditoría unificada:** ajustes y reglas configurables se registran en la misma `MemoriaCalculo` del cálculo (ADR-006/ADR-010), de modo que la OFA resultante es reconstruible.

## Alternativas consideradas
1. **Exigir levantar las ~40 reglas antes de construir.** Descartado por el negocio (RN-070): la información no existe y la persona que la tenía ya no está; bloquearía el proyecto.
2. **Permitir solo ajuste manual, sin promoción.** Descartado: el conocimiento quedaría atrapado en ajustes sueltos; la promoción convierte lo recurrente en configuración y reduce el trabajo manual con el tiempo.
3. **Reglas configurables con un DSL/script ejecutable por el usuario.** Descartado en 1ª etapa: riesgo de que un usuario introduzca lógica no auditable; se limita a condición→acción parametrizada.

## Consecuencias
- El Satélite factura desde el día uno aunque no tenga todas las reglas; absorbe lo no escrito sin bloquear (objetivo central del negocio).
- Riesgo declarado (verificación §3): que "todo se resuelva manual". Se mitiga con la detección de recurrencia y la promoción a configuración, y con métricas de cuántos ajustes manuales hay por período.
- Mantiene el determinismo y la auditabilidad del monto (el ajuste es un dato registrado, no una caja negra).

## Reglas / requerimientos relacionados
REQ-006, REQ-005, REQ-011, RN-070, RN-053, RN-054, BR-023, PQ-026, RNF-01, RNF-03.
