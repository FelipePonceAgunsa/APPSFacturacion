# -*- coding: utf-8 -*-
"""Base local de ZARPE - FACTURACION SIN Docker, sobre un PostgreSQL ya instalado (por ejemplo, el servicio de Windows).

Hace lo mismo que db/recrear-con-migracion.ps1, pero con los binarios de PostgreSQL instalados:
  1. borra y crea la base zarpe_facturacion (BORRA sus datos; no toca otras bases del servidor);
  2. aplica la migracion de EF Core (dotnet ef database update);
  3. crea zarpe_referencia con db/init/01-esquema.sql y 02-ajustes-etapa1.sql;
  4. compara los dos esquemas con pg_dump -s y falla si hay diferencias;
  5. corre db/pruebas/pruebas-etapa1.sql y falla si alguna prueba falla;
  6. carga las semillas de db/semillas/*.sql (datos de prueba inventados);
  7. borra zarpe_referencia.

Uso (desde la raiz del repositorio). La clave va por variable de entorno, nunca en el repositorio:
    $env:ZARPE_DB_PASSWORD = '<clave>'
    python db/local_sin_docker.py --puerto 5432
Opciones: --host (localhost), --puerto (5432), --usuario (postgres), --solo-estado.
Requiere: PostgreSQL 15+ (binarios en C:\\Program Files\\PostgreSQL\\<version>\\bin o en PG_BIN) y
dotnet-ef 10.0.15 (dotnet tool install --global dotnet-ef --version 10.0.15).
"""
import argparse, difflib, glob, os, re, subprocess, sys

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DB = os.path.join(RAIZ, "db")
BACKEND = os.path.join(RAIZ, "Fuentes", "AEXSA.ZARPE.BACKEND.NET")
BASE, REFERENCIA = "zarpe_facturacion", "zarpe_referencia"


def carpeta_bin():
    if os.environ.get("PG_BIN"):
        return os.environ["PG_BIN"]
    candidatas = sorted(glob.glob(r"C:\Program Files\PostgreSQL\*\bin"), key=lambda p: int(re.sub(r"\D", "", p.split(os.sep)[-2]) or 0))
    if not candidatas:
        fallar("No encontre PostgreSQL. Indica la carpeta de binarios en la variable PG_BIN.")
    return candidatas[-1]


def fallar(mensaje):
    print(f"ERROR: {mensaje}", flush=True)
    sys.exit(1)


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--host", default="localhost")
    p.add_argument("--puerto", default="5432")
    p.add_argument("--usuario", default=os.environ.get("ZARPE_DB_USER", "postgres"))
    p.add_argument("--solo-estado", action="store_true", help="Solo prueba la conexion y lista las bases")
    a = p.parse_args()

    clave = os.environ.get("ZARPE_DB_PASSWORD")
    if not clave:
        fallar("Falta la clave: define la variable de entorno ZARPE_DB_PASSWORD.")
    exe = ".exe" if os.name == "nt" else ""
    binarios = carpeta_bin()
    psql, pg_dump = os.path.join(binarios, "psql" + exe), os.path.join(binarios, "pg_dump" + exe)
    env = dict(os.environ, PGPASSWORD=clave, PGOPTIONS="--client-min-messages=warning")
    conexion = ["-h", a.host, "-p", a.puerto, "-U", a.usuario]

    def correr(args, cwd=None):
        return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace", env=env, cwd=cwd)

    def sql(db, sentencia):
        r = correr([psql, *conexion, "-d", db, "-v", "ON_ERROR_STOP=1", "-q", "-c", sentencia])
        if r.returncode:
            fallar(f"psql en {db}: {sentencia}\n{r.stderr.strip()}")
        return r.stdout

    def archivo(db, ruta):
        r = correr([psql, *conexion, "-d", db, "-v", "ON_ERROR_STOP=1", "-q", "-f", ruta])
        if r.returncode:
            print(r.stdout, r.stderr)
            fallar(f"psql -f {ruta} en {db}")
        return r.stdout

    def esquema(db):
        r = correr([pg_dump, *conexion, "-s", "--no-owner", "--no-privileges", '--exclude-table="__EFMigrationsHistory"', db])
        if r.returncode:
            fallar(f"pg_dump de {db}\n{r.stderr.strip()}")
        prefijos = ("--", "SET ", "SELECT pg_catalog.set_config", "\\restrict", "\\unrestrict")
        return [l for l in r.stdout.splitlines() if l.strip() and not l.startswith(prefijos)]

    version = sql("postgres", "select version()").strip()
    print(f"Conectado a {a.host}:{a.puerto} como {a.usuario}: {version.splitlines()[0][:60]}", flush=True)
    bases = sql("postgres", "select string_agg(datname, ', ' order by datname) from pg_database where not datistemplate")
    print(f"Bases del servidor: {bases.strip()}", flush=True)
    if a.solo_estado:
        return

    print(f"1. Recreando la base {BASE} (vacia)...", flush=True)
    sql("postgres", f"DROP DATABASE IF EXISTS {BASE} WITH (FORCE)")
    sql("postgres", f"CREATE DATABASE {BASE}")

    print("2. Aplicando la migracion de EF Core...", flush=True)
    cadena = f"Host={a.host};Port={a.puerto};Database={BASE};Username={a.usuario};Password={clave}"
    env["PATH"] = os.path.join(os.path.expanduser("~"), ".dotnet", "tools") + os.pathsep + env.get("PATH", "")
    r = correr(["dotnet", "ef", "database", "update", "--project", "Satelite.Infraestructura",
                "--startup-project", "Satelite.Api", "--connection", cadena], cwd=BACKEND)
    if r.returncode:
        print(r.stdout[-2000:], r.stderr[-2000:])
        fallar("dotnet ef database update (instalar con: dotnet tool install --global dotnet-ef --version 10.0.15)")
    print("   migracion aplicada", flush=True)

    print(f"3. Creando la base de referencia {REFERENCIA} con db/init (01 + 02)...", flush=True)
    sql("postgres", f"DROP DATABASE IF EXISTS {REFERENCIA} WITH (FORCE)")
    sql("postgres", f"CREATE DATABASE {REFERENCIA}")
    archivo(REFERENCIA, os.path.join(DB, "init", "01-esquema.sql"))
    archivo(REFERENCIA, os.path.join(DB, "init", "02-ajustes-etapa1.sql"))

    print("4. Comparando esquemas (pg_dump -s)...", flush=True)
    de_migracion, de_referencia = esquema(BASE), esquema(REFERENCIA)
    diferencias = [l for l in difflib.unified_diff(de_referencia, de_migracion, "db/init", "migracion", lineterm="", n=0)
                   if l.startswith(("+", "-")) and not l.startswith(("+++", "---"))]
    if diferencias:
        print("\n".join(diferencias[:80]))
        sql("postgres", f"DROP DATABASE IF EXISTS {REFERENCIA} WITH (FORCE)")
        fallar(f"{len(diferencias)} diferencias de esquema (- solo en db/init, + solo en la migracion)")
    print(f"   0 diferencias de esquema ({len(de_migracion)} lineas comparadas)", flush=True)

    print("5. Pruebas SQL (db/pruebas/pruebas-etapa1.sql)...", flush=True)
    salida = archivo(BASE, os.path.join(DB, "pruebas", "pruebas-etapa1.sql")).splitlines()
    for l in salida:
        if re.search(r"\|\s*FALLA\s*\|", l):
            print("  ", l)
    totales = [m for m in (re.match(r"^\s*(\d+)\s*\|\s*(\d+)\s*$", l) for l in salida) if m]
    if not totales:
        print("\n".join(salida[-40:]))
        fallar("no se encontro el resumen de las pruebas")
    ok, fallas = int(totales[-1].group(1)), int(totales[-1].group(2))
    print(f"   ok = {ok}, fallas = {fallas}", flush=True)
    if fallas:
        fallar(f"{fallas} pruebas SQL fallaron")

    print("6. Semillas (db/semillas/*.sql)...", flush=True)
    for s in sorted(glob.glob(os.path.join(DB, "semillas", "*.sql"))):
        archivo(BASE, s)
        print(f"   {os.path.basename(s)}", flush=True)

    print(f"7. Borrando la base de referencia {REFERENCIA}...", flush=True)
    sql("postgres", f"DROP DATABASE IF EXISTS {REFERENCIA} WITH (FORCE)")
    print(f"Listo: {BASE} en {a.host}:{a.puerto}, esquema igual a db/init, ok = {ok}, fallas = {fallas}.", flush=True)


if __name__ == "__main__":
    main()
