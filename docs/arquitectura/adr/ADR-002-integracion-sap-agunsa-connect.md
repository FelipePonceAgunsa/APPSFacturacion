# ADR-002 — Integración con SAP B1 vía Agunsa Connect (Outbox + adaptadores)

Estado: **Propuesto** (depende de PA-001, PA-002)
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
Es el **bloqueante técnico principal** (handoff §3). Verificado en código: el único canal SAP↔AEXSA (`API/SAP/GetFactura`) es de **entrada/conciliación** (SAP→AEXSA); **no existe canal de salida** para crear la orden de venta/OFA en SAP ni recibir el folio (RN-076, BR-027, PQ-029). Crear ese canal es desarrollo nuevo, probablemente de un proveedor externo, y debe pasar por el middleware corporativo **Agunsa Connect** y su gobierno de seguridad/arquitectura (RNF-05, BR-028, PQ-030). AEXSA no tiene desarrollo interno con SAP B1.

El requerimiento (RNF-07) exige que **toda OFA entregada termine con folio o con error explícito**, con reintentos y control de estado. La vía concreta (Service Layer, DI API, o API del proveedor) no está confirmada (PA-001).

## Decisión
Aislar toda la comunicación saliente hacia SAP detrás de un **puerto `ISapOrdenVentaPort`** y materializar la entrega con el **patrón Outbox transaccional**, de modo que el Satélite funcione y se pruebe **sin depender de que la vía SAP esté resuelta**.

1. **Puerto de salida `ISapOrdenVentaPort`** (en la capa de aplicación): `CrearOrdenVenta(OfaDto) → ResultadoEmision{ folio, numeroInterno, estado }`. El dominio no conoce SAP ni Agunsa Connect.
2. **Outbox transaccional:** al confirmar una OFA, en la **misma transacción** de PostgreSQL se escribe la OFA y un registro `OutboxMessage` (tipo `OFA_PENDIENTE`). Un **despachador** (worker en background) lee el outbox y llama al adaptador. Nunca llamada síncrona dentro del request, nunca fire-and-forget (estándar corporativo).
3. **Adaptador `AgunsaConnectSapAdapter`** implementa el puerto y encapsula el protocolo real de Agunsa Connect (a confirmar, PA-002). Se define un **contrato versionado** (`contrato-ofa-sap-v1`) independiente del transporte.
4. **Idempotencia:** cada OFA lleva una **clave de idempotencia** (`OfaId` + versión). Reenviar la misma OFA no crea una segunda orden de venta en SAP; el adaptador y/o SAP deduplican por esa clave.
5. **Reintentos con backoff** exponencial y tope; tras agotar reintentos la OFA queda `EMITIDA_SIN_CONFIRMACION` y se **alerta** (RNF-07, REQ-009).
6. **Recepción del folio:** por **callback** de Agunsa Connect a un endpoint del Satélite (`POST /api/sap/callbacks/folio`) o por polling de estado, según lo que soporte el middleware (PA-002). Al llegar el folio se cierra el vínculo operación↔factura (REQ-009).
7. **Conciliación de entrada:** se mantiene el canal tipo `GetFactura` (DTE en XML) para **conciliar** lo calculado contra el DTE real por folio; no se usa para emitir (RN-076, ver ADR-004).

Todo mensaje registra request, response y error (log auditable), con reproceso manual desde la interfaz sin tocar la base de datos (estándar corporativo de integraciones).

## Alternativas consideradas
1. **Llamada síncrona directa Satélite→SAP dentro del request de confirmación de OFA.** Descartada: contradice el estándar corporativo (nunca síncrono ni fire-and-forget), acopla la disponibilidad de SAP al flujo del usuario, y no da reintento/reproceso resilientes.
2. **Escritura directa en la BD de SAP B1.** Descartada: rompe la integridad del ERP, viola el gobierno de SAP y no emite el DTE correctamente.
3. **Reutilizar `GetFactura` como canal de salida.** Imposible: es de entrada (verificado en código, RN-076). Se mantiene solo para conciliación.
4. **Esperar a que el proveedor defina la API antes de diseñar.** Descartada: bloquearía todo el proyecto. El puerto + Outbox permiten construir y probar con un adaptador simulado (`stub`) y cambiar solo el adaptador cuando PA-001/PA-002 se resuelvan.

## Consecuencias
- El Satélite se construye y prueba de punta a punta con un **adaptador simulado** de SAP; la pieza externa (proveedor + Agunsa Connect) se enchufa después sin tocar el dominio ni los casos de uso. Esto separa lo que el desarrollo acelera (el Satélite) de lo que depende de terceros (la integración real), según el principio de no prometer aceleración donde no la hay.
- La resiliencia (folio-o-error, RNF-07) queda garantizada por diseño (Outbox + reintentos + estado + alerta).
- Dependencia abierta: el contrato real de Agunsa Connect (PA-002) y la vía de escritura en SAP (PA-001) deben confirmarse antes de producción; hasta entonces el ADR queda **Propuesto**.
- Se requiere un worker de background (despachador de Outbox) en el despliegue (ECS service aparte o hosted service en el mismo contenedor).

## Reglas / requerimientos relacionados
REQ-008, REQ-009, RNF-05, RNF-07, RN-059, RN-060, RN-076, BR-027, BR-028, PQ-029, PQ-030, PA-001, PA-002.
