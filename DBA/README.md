# DBA — revisión del modelo de datos de ZARPE - FACTURACIÓN

Carpeta para quien administra la base de datos. Reúne las observaciones del DBA, cómo se corrigieron y las pruebas para comprobarlo.

- **Fuente del modelo:** `docs/arquitectura/modelo-datos.md` (versión 1.1). El DDL vive ahí; esta carpeta no lo duplica. Cada cambio está marcado en el DDL como `-- [DBA-n]` (primera revisión) o `-- [DBA-nb]` (segunda revisión).
- **Contrato del evento de AEXSA:** `docs/arquitectura/contratos-integracion.md` §2 y `docs/arquitectura/api/openapi.yaml` (esquema `EventoFacturable`).
- **Motor:** PostgreSQL 15 o superior (estándar: RDS PostgreSQL 16). Se necesita por `NULLS NOT DISTINCT`.

## Contenido

| Archivo | Para qué |
|---|---|
| `README.md` | Este documento: observaciones, correcciones y cómo correr las pruebas |
| `probar_ddl.py` | Crea un PostgreSQL temporal, ejecuta el DDL completo de `modelo-datos.md` y corre 22 pruebas (casos que la base debe rechazar y casos que debe aceptar). Borra la instancia al terminar |
| `validar_contrato.py` | Compara campo por campo el mensaje de AEXSA en `contratos-integracion.md` con el esquema del OpenAPI |

Los datos de las pruebas son inventados.

## Segunda revisión (2026-11-05)

| Observación | Corrección | Prueba |
|---|---|---|
| **1** Una versión antigua podía aplicarse después de una nueva | Algoritmo de ingesta en `modelo-datos.md` §4b: bloqueo de la operación con `SELECT … FOR UPDATE` y comparación de versiones. Además, el trigger `tr_operacion_version_no_retrocede` impide que `version_evento` baje. La versión vieja queda `RECHAZADO` con `VERSION_SUPERADA`, y AEXSA recibe `409` y no reintenta. Cada mensaje trae el estado completo, así que perder una versión intermedia no importa | Bajar de v3 a v2: rechazado. v3 a v4: pasa. Aplicar dos veces la misma versión: rechazado |
| **3 y 4** Importar solo listas creaba una versión sin artículos | Cada versión del catálogo queda completa: una importación parcial copia desde la versión vigente (`base_version`) lo que no vino. La primera versión debe traer artículos. Los kits quedan con FK al artículo de su misma versión | Precio sin artículos copiados: rechazado. Con la copia: pasa. Kit con un artículo inexistente: rechazado |
| **6** Faltaba exigir `agregada_por` | `CHECK ck_linea_agregada_manual`: exige quién y motivo en las líneas agregadas a mano, y prohíbe esos datos en las demás | Sin quién o sin motivo: rechazado. Completa: pasa |
| **8** Faltaba exigir motivo en `DECISION` | `CHECK ck_pendiente_decision_con_motivo`: motivo no vacío y la decisión tomada. `CHECK ck_pendiente_resolucion_completa`: fecha, usuario y tipo de resolución van juntos. Lo mismo para las omisiones descartadas | Sin motivo o con motivo en blanco: rechazado |
| **10** Alertas duplicadas con `entidad_id` nulo | El índice `ux_alerta_abierta_entidad` usa `NULLS NOT DISTINCT` | La segunda alerta igual abierta: rechazada. Una vez cerrada la anterior: pasa |
| **Contrato AEXSA** | `contratos-integracion.md`: se quitó la interfaz pull y se agregó la tabla de respuestas. OpenAPI: respuestas 201, 200, 409 y 400 con el esquema `ResultadoIngesta`. `sentido` y `tipoRetiro` quedan obligatorios aunque puedan ir vacíos; también `cantidad` del servicio y `numero` del contenedor | `validar_contrato.py`: 0 diferencias. Los 4 ejemplos del paquete de AEXSA siguen válidos |

## Primera revisión (2026-11-05)

Los 11 vacíos iniciales (UNIQUE de `evento_aexsa`, tablas de artículos e importaciones, vigencias de precios, relación línea–memoria, origen de la línea, recálculo de la OFA, pendientes, enums, omisiones y alertas, FK y CHECK faltantes) y su decisión están en la tabla del encabezado de `modelo-datos.md`.

## Cómo correr las pruebas

Requisitos: Python 3, PostgreSQL 15 o superior instalado localmente (solo se usan sus binarios) y, para `validar_contrato.py`, el paquete `ruamel.yaml`.

```powershell
# Desde la raíz del repositorio
python DBA/probar_ddl.py
python DBA/validar_contrato.py
```

- `probar_ddl.py` usa los binarios de `C:\Program Files\PostgreSQL\<versión>\bin`. En otra ruta o en Linux, indica la carpeta con la variable `PG_BIN`. Crea la instancia en la carpeta temporal del sistema, en el puerto `55432` y solo en `localhost`; no toca ninguna base existente. Termina con código distinto de cero si alguna prueba falla.
- Resultado esperado: `ddl y pruebas: OK`, `pruebas OK: 22 de 22` y `tablas creadas: 33`. Última corrida: 2026-11-05, PostgreSQL 16, todo en verde.

## Pendientes para el DBA

- Confirmar la regla del recálculo: las líneas agregadas a mano se copian a la versión nueva y los ajustes de monto no (DBA-7).
- Definir particionamiento por fecha de `evento_auditoria` y `memoria_calculo` si crecen (`modelo-datos.md` §5).
- Las migraciones reales se escriben en EF Core a partir de este DDL (tarea 0.2 de `tasks.md`).
