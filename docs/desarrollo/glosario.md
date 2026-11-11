# Glosario del negocio — para el desarrollador

Versión: 1.0 · Fecha: 2026-11-04
Los términos que verás en el código, los ADR y las reglas de negocio. Escrito para quien no conoce el negocio portuario.

## Los sistemas
| Término | Qué es |
|---|---|
| **AEXSA SYS** | Sistema operacional actual (.NET 4.6/4.8 y SQL Server). Origen de los eventos facturables. Se integra, no se reemplaza |
| **ZARPE - FACTURACIÓN** (el "Satélite") | Lo que vas a construir. Es el nombre del producto; "Satélite" es el nombre de trabajo anterior y se sigue usando en los documentos técnicos y en el código (`Satelite.*`). Calcula la prefactura, guarda los convenios y da trazabilidad |
| **SAP B1** (SAP Business One) | ERP donde se emite la factura electrónica y se concilia el pago |
| **Agunsa Connect** | Middleware corporativo por donde debe pasar toda integración con SAP. Su contrato técnico está abierto (PA-002) |
| **Stub / simulador** | Adaptador falso de un tercero (SAP, AEXSA, Agunsa Connect) para construir y probar sin esperarlo |

## El flujo
| Término | Qué es |
|---|---|
| **Evento facturable** | Aviso que AEXSA SYS envía cuando el visador aprueba una operación. Es el comienzo del ciclo |
| **Visado** | Control documental previo a facturar. Lo hace el visador |
| **OFA** (Orden de Facturación) | La prefactura que arma el Satélite, valorizada y validada. En SAP equivale a una **orden de venta**: igual a la factura pero sin folio |
| **Prefactura valorizada** | La OFA con sus líneas y montos ya calculados. En la Fase A el facturador la digita en SAP |
| **Folio / DTE** | Número y documento tributario electrónico que SAP asigna al emitir. El folio une la operación, la factura y el pago |
| **Check de salida** | Marca en AEXSA SYS que permite liberar la carga una vez emitida la factura |
| **Facturador** | Persona que revisa y emite la factura. Conoce reglas que no están escritas en ningún lado |

## La operación
| Término | Qué es |
|---|---|
| **Operación** | Un movimiento de carga facturable. Se identifica por `operacionIdAexsa` |
| **Contenedor / carga suelta** | Tipos de carga. La carga suelta (LCL) es de la 2ª etapa |
| **Retiro directo / indirecto** | Directo: el contenedor sale del puerto sin entrar a AEXSA. Indirecto: entra al almacén. Un directo puede pasar a indirecto si aduana ordena una inspección |
| **Ruta** | Combinación de {tipo de carga} × {tipo de retiro} × {con o sin convenio}. Define cómo se cobra |
| **DIN** | Declaración de ingreso (importación). Define el consignatario y se contrasta con la papeleta |
| **DUS** | Documento único de salida (exportación). En exportación es la unidad de cobro: se cobra por cantidad de DUS |
| **Papeleta** | Documento que se contrasta con la DIN en la revisión documental |
| **BL** | Conocimiento de embarque. Una factura puede agrupar varios BL |
| **Consignatario** | Destinatario por defecto de la factura, según la DIN |
| **Forwarder** | Agente de carga. A veces la factura (o parte de ella) va a él por instrucción comercial |
| **Repartido** | Una operación facturada a más de un destinatario. Capacidad nueva que el sistema actual no tiene |
| **Facturación complementaria** | Factura adicional cuando un cargo del puerto llega tarde |

## Convenios y tarifas
| Término | Qué es |
|---|---|
| **Convenio** | Condiciones y tarifas pactadas con un cliente. Vive en el Satélite, versionado |
| **Versión de convenio** | Cada cambio genera una versión nueva. Estados: borrador, vigente, vencida, reemplazada |
| **Tarifa pública / kit spot** | Tarifa que se usa sin convenio o para un ítem que el convenio no cubre. Vive en SAP |
| **Excepción** | Línea que usó tarifa pública dentro de un convenio. Se marca |
| **Tipo de cambio mínimo (piso)** | El dólar del convenio nunca se aplica por debajo de este valor (RN-022) |
| **Días libres** | Días de almacenaje sin cargo que define el convenio. Se descuentan antes de cobrar |
| **Aforo** | Inspección de aduana. Mientras dura, no se cobra almacenaje |
| **SAG** | Inspección fitosanitaria. Se cobra junto con "uso de área" |
| **Security fee** | Cargo de seguridad del puerto, un concepto fijo del contenedor directo |
| **Refacturación** | Recobro de un gasto que AEXSA pagó a un tercero. Lleva un recargo de 15% (RN-024). En el directo es un ítem propio |
| **STI / DP World** | Los dos terminales del puerto. En STI la refacturación del directo tiene valor; en DP World va en cero (ADR-015) |
| **Falso flete** | Recargo de 90% si un servicio programado se cancela durante su ejecución |
| **Mayor m³ o tonelada** | En ciertos conceptos de carga suelta se cobra el mayor de los dos |

## El catálogo de SAP
| Término | Qué es |
|---|---|
| **Artículo SAP** | Un servicio que SAP sabe vender. Tiene código y unidad, sin precio |
| **Lista de precios** | Objeto de SAP, aparte de los artículos, que aporta el precio por unidad |
| **Kit** | "Bolsa" de artículos que se venden y cobran juntos. El facturador elige kit + lista de precios y SAP despliega los artículos con su precio. En el Satélite es **referencia**, no entrada del cálculo |
| **Snapshot de catálogo** | Copia versionada del catálogo de SAP. El cálculo usa el snapshot, no la llamada en vivo, para que sea reproducible |
| **Vínculo servicio↔artículo** | Relación entre un servicio del Satélite y su artículo SAP. Lo confirma una persona |
| **Pago Recibido** | Asiento de SAP que registra un pago y lo concilia con la factura |

## Conceptos de diseño
| Término | Qué es |
|---|---|
| **Determinístico** | Mismo dato de entrada, mismo resultado, siempre. Es el requisito del cálculo de montos |
| **Memoria de cálculo** | Registro inmutable de qué reglas, versiones, dólar y ajustes produjeron cada monto. Permite reconstruirlo |
| **Append-only** | Tablas donde solo se agregan filas; nunca se modifican ni se borran |
| **Outbox** | Patrón para enviar a SAP: el mensaje se guarda en la misma transacción que la OFA y un proceso aparte lo despacha con reintentos |
| **Idempotencia** | Reenviar el mismo mensaje no crea un segundo documento. Clave: `OfaId` + versión |
| **Puerto y adaptador** | Arquitectura hexagonal: el dominio define puertos (interfaces) y la infraestructura los implementa con adaptadores |
| **Fase A / Fase B** | A: valor sin terceros (prefactura para digitar). B: escritura en SAP, folio y pagos, que dependen de terceros |
| **PA / PQ / BR / RN / REQ / ADR** | Pregunta de arquitectura / pregunta del levantamiento / brecha / regla de negocio / requerimiento / decisión de arquitectura |
