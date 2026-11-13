# Base local de ZARPE - FACTURACIÓN

PostgreSQL 16 en Docker, con el esquema de `docs/arquitectura/modelo-datos.md` y los ajustes de la etapa 1. Solo para desarrollo local.

| Dato | Valor |
|---|---|
| Contenedor | `zarpe-postgres` |
| Host / puerto | `localhost:5433` (el 5432 lo usa `hc-postgres` de Huella) |
| Base | `zarpe_facturacion` |
| Usuario / clave | `postgres` / `postgres` (se cambian con `ZARPE_DB_USER` y `ZARPE_DB_PASSWORD`) |

Cadena de conexión para EF Core (Npgsql):

```
Host=localhost;Port=5433;Database=zarpe_facturacion;Username=postgres;Password=postgres
```

## Qué se crea

| Script | Contenido | Dueño |
|---|---|---|
| `init/01-esquema.sql` | DDL de `modelo-datos.md`, **generado** con `generar-esquema.ps1`. No se edita a mano | DBA (vía `modelo-datos.md`) |
| `init/02-ajustes-etapa1.sql` | Ajustes de la etapa 1 aún no incorporados al modelo (marcas `[E1-n]`). Re-ejecutable | Propuesta para el DBA. Se borra cuando el modelo los incluya |

Ajustes de la etapa 1 (`02`):

| Marca | Qué hace |
|---|---|
| E1-1 | Ingesta de AEXSA: un mensaje `RECHAZADO` lleva `codigo_error` (p. ej. `VERSION_SUPERADA`, `ENTIDAD_NO_HABILITADA`). El trigger de versión y el algoritmo ya están en el modelo (DBA-1b) |
| E1-3 | Tipo de cambio: fuente `API` / `ARCHIVO` / `MANUAL` (antes `API_SAP`), coherente con `importacion_id`. Gana el valor de SAP: un manual no lo reemplaza (trigger) |
| E1-4 | Ítem agregado a mano: `agregada_en` (cuándo). Quién y por qué ya los exige el modelo (DBA-6b) |

E1-2 (catálogo completo por versión) y E1-5 (CHECK de pendientes) se quitaron porque la segunda revisión del DBA ya los incluye en el modelo (DBA-3b y DBA-8b). Las pruebas siguen cubriéndolos.
| E1-6 | Contrato de entrada de AEXSA v3.0: `entidad` y `ids_aexsa` (clave única por entidad; visado virtual con un solo ID); `entidad` en la clave de `evento_aexsa`; tabla `operacion_bl` en lugar de `documento_carga_id` y `bls`; `din` y `papeletas` como arreglos de números; sin columnas de aforo (llega en `inspecciones`, con `resultado` validado) |
| E1-7 | Inspecciones: tabla `tipo_inspeccion_mapa` (código de AEXSA → `AFORO_ADUANA` / `SAG` / `OTRA` / `SIN_MAPEAR`) y pendientes `INSPECCION_NO_FINALIZADA` e `INSPECCION_RECHAZADA` (RN-058) |

Fuera de la etapa 1, sin ajustes: omisiones, alertas y reglas configurables.

## Comandos (desde la carpeta `db`)

```powershell
docker compose up -d          # levantar
docker compose stop           # detener, conserva los datos
docker compose down -v        # borrar contenedor y datos
docker exec -it zarpe-postgres psql -U postgres -d zarpe_facturacion   # consola SQL

# Pruebas de las reglas (no dejan datos: todo se revierte)
Get-Content .\pruebas\pruebas-etapa1.sql -Raw | docker exec -i zarpe-postgres psql -U postgres -d zarpe_facturacion -q

# Validar el OpenAPI (YAML, referencias rotas y esquema del evento de AEXSA). Requiere Python con PyYAML
python .\pruebas\validar-openapi.py
```

## Cuando cambie el modelo de datos

```powershell
.\generar-esquema.ps1
docker compose down -v
docker compose up -d
Get-Content .\pruebas\pruebas-etapa1.sql -Raw | docker exec -i zarpe-postgres psql -U postgres -d zarpe_facturacion -q
```

Los scripts de `init/` solo corren cuando el volumen está vacío; por eso hay que borrar el volumen (`down -v`), y eso **borra los datos locales**. Si el modelo nuevo ya incluye un ajuste de `02`, el script no falla (es re-ejecutable); revisa igual que no queden dos versiones de la misma regla.

Cuando exista la migración inicial de EF Core, esa pasa a ser la fuente del esquema y estos scripts quedan solo como referencia.
