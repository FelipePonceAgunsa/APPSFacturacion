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

## La migración de EF Core es la fuente del esquema

Desde la etapa 1 el esquema de la base lo crea la migración inicial de EF Core
(`Fuentes/AEXSA.ZARPE.BACKEND.NET/Satelite.Infraestructura/Persistencia/Migraciones`). Esa migración ejecuta
copias congeladas de `init/01-esquema.sql` e `init/02-ajustes-etapa1.sql` (en `Migraciones/Sql/`), así que los
CHECK, triggers e índices parciales son exactamente los mismos. Los scripts de `init/` quedan como referencia y
siguen creando la base cuando el volumen de Docker está vacío.

Para recrear la base local con la migración y comprobar que equivale a `init/` (**borra los datos locales**):

```powershell
& .\recrear-con-migracion.ps1 *>&1
```

El script:
1. borra y crea `zarpe_facturacion`;
2. aplica la migración con `dotnet ef database update` (clave: `ZARPE_DB_PASSWORD`, por defecto `postgres`);
3. crea `zarpe_referencia` con `init/01` y `init/02`;
4. compara los dos esquemas con `pg_dump -s` (sin `__EFMigrationsHistory`) y falla si hay diferencias;
5. corre `pruebas/pruebas-etapa1.sql` y falla si alguna prueba falla;
6. carga, en orden de nombre, las semillas de `semillas/*.sql` (datos de prueba inventados). Van después de las pruebas porque las pruebas insertan algunos de esos mismos datos dentro de una transacción que se revierte;
7. borra `zarpe_referencia`.

Resultado esperado: `0 diferencias de esquema` y `ok = 63, fallas = 0`.

Las semillas cargan solo tipos de servicio, terminales y tipos de inspección. El catálogo SAP, los vínculos, los
convenios y los eventos de AEXSA de ejemplo se cargan por la API con `datos-prueba/e2e-etapa1.ps1` (ver
`datos-prueba/README.md`). Si la API estaba corriendo, hay que reiniciarla después de recrear la base: el script corta
las conexiones abiertas. Los bloques 4 y 5 de la etapa 1 no agregaron cambios de esquema `[E1-n]`.

## Cuando cambie el esquema

Un cambio que pide el código se agrega a `init/02-ajustes-etapa1.sql` con su marca `[E1-n]` (re-ejecutable) **y** a
una migración nueva de EF Core con el mismo SQL. La migración inicial no se modifica.

Cuando cambie `modelo-datos.md` (DBA):

```powershell
.\generar-esquema.ps1
docker compose down -v
docker compose up -d
Get-Content .\pruebas\pruebas-etapa1.sql -Raw | docker exec -i zarpe-postgres psql -U postgres -d zarpe_facturacion -q
```

Los scripts de `init/` solo corren cuando el volumen está vacío; por eso hay que borrar el volumen (`down -v`), y eso **borra los datos locales**. Si el modelo nuevo ya incluye un ajuste de `02`, el script no falla (es re-ejecutable); revisa igual que no queden dos versiones de la misma regla. Si el modelo cambia, también hace falta una migración nueva de EF Core con la diferencia.
