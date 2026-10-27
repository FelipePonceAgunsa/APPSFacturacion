# ADR-004 — Retorno de facturación a AEXSA SYS

Estado: **Propuesto** (depende de PA-004, PA-008)
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
El ciclo no termina en SAP: una vez emitida la factura, el resultado debe volver a AEXSA SYS para habilitar el **check de salida** que libera la carga (RN-072). Hoy existe el canal `API/SAP/GetFactura` por el que SAP devuelve a AEXSA el DTE completo en XML Base64 (folio, TipoDTE, emisor, receptor, totales; distingue exportación), persistido en `GestorPagosFactura*`, uniendo por folio (`SolicitudVisado.PagoNumeroDocumento = GestorPagosFactura.Folio`, RN-074, RN-029). Ese canal **no incluye PDF** (solo XML) y es de **entrada** a AEXSA. Los datos exactos a retornar y la vía están por confirmar (PQ-032, PA-004); el acceso al XML/PDF del software de facturación electrónica también (PA-008, RN-062).

## Decisión
El Satélite **cierra el lazo devolviendo a AEXSA SYS el folio y los datos de la factura** a través de un puerto `IAexsaRetornoPort`, reutilizando el mecanismo de conciliación por folio que ya existe, y usa ese mismo folio como **clave de cuadratura** de toda la cadena.

1. **Puerto `IAexsaRetornoPort`:** `NotificarFacturaEmitida(operacionId, folio, tipoDte, montos)`. El Satélite no asume la vía concreta.
2. **Vía preferida:** reutilizar/extender el canal `GetFactura` de conciliación (RN-074) para que el registro del folio en AEXSA SYS habilite el check de salida (RN-072). Si TI-AEXSA prefiere un endpoint nuevo o escritura controlada, solo cambia el adaptador (PA-004).
3. **Unión por folio:** el folio es la clave que cuadra `operación (AEXSA) ↔ OFA (Satélite) ↔ factura (SAP) ↔ pago` (RNF-02, cuadratura obligatoria de los principios).
4. **Conciliación del DTE:** el Satélite recibe el DTE (XML) por el canal de entrada y **concilia** lo calculado contra el DTE real (montos, receptor); discrepancias se alertan (apoya REQ-007/REQ-011).
5. **PDF:** si el PDF no está disponible por este canal (solo XML, RN-076), el Satélite guarda el XML y referencia el documento; el acceso al PDF queda sujeto a PA-008.
6. Idempotencia: notificar dos veces el mismo folio no duplica el check de salida.

## Alternativas consideradas
1. **Que el Satélite escriba directo en la BD de AEXSA SYS** para marcar el check de salida. Descartada como opción por defecto: acopla el Satélite al esquema interno del legacy (con credenciales en claro, BR-016) y salta las validaciones de AEXSA. Solo se consideraría si TI-AEXSA no ofrece un endpoint y lo autoriza explícitamente.
2. **No retornar a AEXSA y liberar la carga desde el Satélite.** Descartada: viola RNF-06 (no disrupción); el check de salida es de AEXSA SYS.
3. **Esperar el PDF del DTE para cerrar el lazo.** Descartada: el folio (XML) basta para habilitar la salida; esperar el PDF (otro sistema, PA-008) retrasaría la liberación.

## Consecuencias
- El lazo se cierra con el folio, sin depender del PDF ni de reescribir el legacy.
- La cuadratura por folio es la columna vertebral de la trazabilidad (RNF-02) y de la detección de omisiones (ADR-009).
- Dependencias abiertas: vía exacta de retorno (PA-004) y disponibilidad del XML/PDF (PA-008). ADR **Propuesto** hasta confirmarlas con Ricardo/TI-AEXSA.

## Reglas / requerimientos relacionados
REQ-009, RN-072, RN-074, RN-029, RN-062, RN-076, PQ-031, PQ-032, PA-004, PA-008.
