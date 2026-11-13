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
| E1-1 | Ingesta de AEXSA: `version_evento` nunca baja (trigger). Un mensaje `RECHAZADO` lleva `codigo_error` (p. ej. `VERSION_SUPERADA`). El algoritmo de ingesta está comentado en el script |
| E1-2 | Catálogo: cada versión queda completa. `base_version` en la importación; la primera carga trae artículos; los kits tienen FK al artículo de su misma versión |
| E1-3 | Tipo de cambio: fuente `API` / `ARCHIVO` / `MANUAL` (antes `API_SAP`), coherente con `importacion_id`. Gana el valor de SAP: un manual no lo reemplaza (trigger) |
| E1-4 | Ítem agregado a mano: exige quién, cuándo y motivo, y los prohíbe en las demás líneas |
| E1-5 | Pendientes: la resolución trae fecha, usuario y tipo juntos; una `DECISION` exige motivo no vacío y la decisión |

Fuera de la etapa 1, sin ajustes: omisiones, alertas y reglas configurables.

## Comandos (desde la carpeta `db`)

```powershell
docker compose up -d          # levantar
docker compose stop           # detener, conserva los datos
docker compose down -v        # borrar contenedor y datos
docker exec -it zarpe-postgres psql -U postgres -d zarpe_facturacion   # consola SQL

# Pruebas de las reglas (no dejan datos: todo se revierte)
Get-Content .\pruebas\pruebas-etapa1.sql -Raw | docker exec -i zarpe-postgres psql -U postgres -d zarpe_facturacion -q
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
