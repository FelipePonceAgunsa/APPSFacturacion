# ADR-015 — Cálculo del contenedor directo (STI vs DP World)

Estado: **Aceptado**
Fecha: 2026-10-28
Decide: Arquitecto de Solución
Origen: Adenda del analista 2026-10-28 (handoff §A1); cierra PQ-006/BR-007 (única regla de monto de negocio que quedaba abierta). Reglas nuevas RN-078, RN-079; corrige RN-007.

## Contexto
El diseño previo (motor de tarifas, basado en el AS-IS/BPMN) modelaba el contenedor **directo** con un "kit directo de SAP" que incluía recepción/despacho, y la regla STI/DPW como "mantener valor del kit / poner refacturación en cero" (RN-007, entonces sin criterio confirmado). La adenda del analista **corrige esto con criterio de negocio confirmado** (Francisco Thomson, 2026-10-28):

- En contenedor **DIRECTO** el contenedor **no entra a AEXSA** (el consignatario lo retira directo del puerto). Por tanto **NO se cobra recepción, ni despacho, ni movilización**.
- Se cobran **exactamente 3 conceptos**:
  1. **Visación** — revisión documental de AEXSA.
  2. **Security fee** — tasa del puerto.
  3. **Refacturación** — recobro del cargo que el puerto hace a AEXSA por **secuenciar el contenedor de día**.
- **Regla fija por terminal (sin excepciones, sin convenio, tarifa pública):**
  - **STI** cobra refacturación + security fee → **refacturación con valor**.
  - **DP World** cobra solo security fee → **refacturación en cero**.
- **Origen del dato del terminal:** se selecciona al crear el manifiesto (Planificación → Itinerario); se refleja en el reporte Recepción/Despacho (campo "terminal de retiro").

## Decisión
Modelar el cálculo del contenedor directo como una **regla determinística de primera clase** en el motor de tarifas (ADR-006), NO como un kit genérico ni como regla configurable pendiente:

1. **Composición fija del directo (RN-078):** la OFA de un directo contiene solo **visación + security fee + refacturación**. El motor no agrega recepción/despacho/movilización para la ruta directa.
2. **Regla por terminal (RN-079):** el valor de la línea de **refacturación** se gobierna por el terminal de la operación:
   - terminal = STI → refacturación con su tarifa pública.
   - terminal = DP World → refacturación = 0.
3. **Determinística y sin convenio:** el directo usa **tarifa pública** (no convenio); el cálculo es fijo y reproducible. Deja de ser un caso "configurable pendiente".
4. **Dato de entrada:** el **terminal de retiro** se incorpora al `ContextoCalculo` (viene del manifiesto, Planificación → Itinerario). Es un dato de la operación, no un parámetro manual.
5. **Precedencia del convenio intacta:** si el consignatario tiene convenio vigente, manda el convenio aunque haya inspección (RN-045); esta regla aplica al directo **sin convenio**, que es el caso de tarifa pública.

## Alternativas consideradas
1. **Mantener el "kit directo" con recepción/despacho (diseño previo).** Descartada: es incorrecto según el negocio confirmado (el contenedor no entra a AEXSA, no hay recepción/despacho). Habría facturado conceptos que no corresponden.
2. **Dejar STI/DPW como regla configurable (nivel 2, ADR-008).** Descartada ahora que el criterio está confirmado y es fijo por terminal: una regla determinística en código es más clara y auditable que una configurable para algo que no cambia. (La capa configurable sigue para lo que sí es variable/no escrito.)
3. **Derivar el terminal de otro dato.** Descartada: el terminal se define explícitamente en el itinerario; se toma de ahí, no se infiere.

## Consecuencias
- El cálculo del directo queda **simple, determinístico y correcto** (3 ítems, refacturación por terminal). Elimina el riesgo de cobrar recepción/despacho indebidos.
- Se agrega **`terminal`** (STI | DP_WORLD | otro) como atributo de la operación en el `ContextoCalculo` y en el modelo de datos.
- Cierra la última regla de monto de negocio abierta (PQ-006): el levantamiento de facturación queda **cerrado** para efectos de cálculo.
- Caso de prueba nuevo en el motor (refacturación STI con valor / DP World en cero).

## Reglas / requerimientos relacionados
REQ-002, REQ-005, RN-078, RN-079, RN-007 (corregida), RN-045, PQ-006, BR-007. Refina ADR-006 (motor de tarifas) y ADR-008 (deja de ser regla configurable).
