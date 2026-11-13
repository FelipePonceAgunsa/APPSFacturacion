# -*- coding: utf-8 -*-
"""Prueba el DDL de docs/arquitectura/modelo-datos.md en una instancia temporal de PostgreSQL.

Crea un cluster desechable en la carpeta temporal del sistema (puerto 55432, solo localhost), ejecuta
todos los bloques ```sql del modelo en orden y luego 23 pruebas: casos que la base debe rechazar y casos
que debe aceptar, uno o mas por cada observacion del DBA. Imprime el resultado, borra el cluster y
termina con codigo 1 si algo falla. No toca ninguna base existente. Los datos son inventados.

Uso (desde la raiz del repositorio):  python DBA/probar_ddl.py
Requiere PostgreSQL 15 o superior instalado (solo sus binarios). Si no esta en
C:\\Program Files\\PostgreSQL\\<version>\\bin, indicar la carpeta en la variable de entorno PG_BIN.
"""
import glob, os, re, shutil, subprocess, sys, tempfile, time

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MD = os.path.join(RAIZ, "docs", "arquitectura", "modelo-datos.md")
PUERTO = "55432"


def carpeta_bin():
    if os.environ.get("PG_BIN"):
        return os.environ["PG_BIN"]
    candidatas = sorted(glob.glob(r"C:\Program Files\PostgreSQL\*\bin"))
    if not candidatas:
        sys.exit("No encontre PostgreSQL. Indica la carpeta de binarios en la variable PG_BIN.")
    return candidatas[-1]


BIN = carpeta_bin()
EXE = ".exe" if os.name == "nt" else ""


def binario(nombre):
    return os.path.join(BIN, nombre + EXE)


def correr(args):
    return subprocess.run(args, capture_output=True, text=True, encoding="utf-8", errors="replace")


def debe_fallar(nombre, sql):
    """Bloque que pasa solo si la sentencia es rechazada por la base."""
    return f"""
DO $p$ BEGIN
  BEGIN
    {sql};
    RAISE EXCEPTION 'NO_FALLO';
  EXCEPTION WHEN OTHERS THEN
    IF SQLERRM = 'NO_FALLO' THEN RAISE EXCEPTION 'FALLA {nombre}: la base debia rechazarlo y lo acepto'; END IF;
    RAISE NOTICE 'OK {nombre} (rechazado: %)', SQLERRM;
  END;
END $p$;"""


def debe_pasar(nombre, sql):
    return f"\n{sql};\nDO $p$ BEGIN RAISE NOTICE 'OK {nombre} (aceptado)'; END $p$;"


# Datos inventados minimos y un caso por cada observacion del DBA (debe pasar / debe fallar).
PRUEBAS = "\n".join([
    """
INSERT INTO tipo_servicio VALUES (1, 'SRV-1', 'Servicio de ejemplo', 'CTR', null, true);
INSERT INTO cliente (id, rut, razon_social) VALUES ('00000000-0000-0000-0000-000000000001', '76.000.000-0', 'Cliente de ejemplo');
INSERT INTO operacion (id, operacion_id_aexsa, documento_carga_id, cliente_id, version_evento)
  VALUES ('00000000-0000-0000-0000-0000000000a1', '900001', 'D-1', '00000000-0000-0000-0000-000000000001', 3);
INSERT INTO ofa (id, operacion_id, estado) VALUES ('00000000-0000-0000-0000-0000000000f1', '00000000-0000-0000-0000-0000000000a1', 'BORRADOR');
""",
    # Observacion 1: versiones del evento
    debe_pasar("1a evento APLICADO v3", "INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('900001', 3, 'VISADO_APROBADO', '{}', 'APLICADO')"),
    debe_pasar("1b mismo mensaje registrado como DUPLICADO", "INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('900001', 3, 'VISADO_APROBADO', '{}', 'DUPLICADO')"),
    debe_fallar("1c aplicar dos veces la misma version", "INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado) VALUES ('900001', 3, 'VISADO_APROBADO', '{}', 'APLICADO')"),
    debe_pasar("1d version vieja registrada como RECHAZADO", "INSERT INTO evento_aexsa (operacion_id_aexsa, version, tipo_evento, carga, resultado, detalle_error) VALUES ('900001', 2, 'OPERACION_ACTUALIZADA', '{}', 'RECHAZADO', 'VERSION_SUPERADA')"),
    debe_fallar("1e version_evento no puede retroceder (3 -> 2)", "UPDATE operacion SET version_evento = 2 WHERE operacion_id_aexsa = '900001'"),
    debe_pasar("1f version_evento avanza (3 -> 4)", "UPDATE operacion SET version_evento = 4 WHERE operacion_id_aexsa = '900001'"),
    # Observaciones 3 y 4: catalogo completo por version
    """
INSERT INTO catalogo_importacion (id, origen, tipo, estado, cargado_por) VALUES ('00000000-0000-0000-0000-0000000000c1', 'ARCHIVO', 'COMPLETO', 'CONFIRMADA', 'prueba');
INSERT INTO catalogo_snapshot (version, origen, importacion_id, confirmado_por, confirmado_en) VALUES (1, 'ARCHIVO', '00000000-0000-0000-0000-0000000000c1', 'prueba', now());
INSERT INTO sap_articulo VALUES (1, 'SRV-REC-001', 'Recepcion', 'CTR', true, null);
INSERT INTO sap_lista_precios VALUES (1, 'LP-PUBLICA', 'Lista publica', true);
INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (1, 'SRV-REC-001', 'LP-PUBLICA', 17000, 'CLP', '2026-01-01');
INSERT INTO catalogo_importacion (id, origen, tipo, estado, cargado_por) VALUES ('00000000-0000-0000-0000-0000000000c2', 'ARCHIVO', 'LISTAS_PRECIOS', 'CONFIRMADA', 'prueba');
INSERT INTO catalogo_snapshot (version, origen, importacion_id, base_version, confirmado_por, confirmado_en) VALUES (2, 'ARCHIVO', '00000000-0000-0000-0000-0000000000c2', 1, 'prueba', now());
""",
    debe_fallar("3a precio en una version sin articulos (sin copiar desde la base)", "INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (2, 'SRV-REC-001', 'LP-PUBLICA', 18000, 'CLP', '2026-02-01')"),
    debe_pasar("3b importacion solo de listas: copia articulos y listas desde la version base",
               "INSERT INTO sap_articulo SELECT 2, codigo, descripcion, unidad_medida, activo, grupo FROM sap_articulo WHERE snapshot_version = 1; "
               "INSERT INTO sap_lista_precios SELECT 2, codigo, nombre, es_publica FROM sap_lista_precios WHERE snapshot_version = 1; "
               "INSERT INTO tarifa_snapshot (version, articulo_id, lista_precios_id, precio, moneda, vigente_desde) VALUES (2, 'SRV-REC-001', 'LP-PUBLICA', 18000, 'CLP', '2026-02-01')"),
    debe_fallar("3c base_version posterior a la version", "UPDATE catalogo_snapshot SET base_version = 2 WHERE version = 1"),
    debe_fallar("3d kit con articulo que no existe en su version",
                "INSERT INTO sap_kit (id, snapshot_version, kit_codigo, nombre) VALUES ('00000000-0000-0000-0000-0000000000d1', 2, 'SPOT', 'Kit spot'); "
                "INSERT INTO sap_kit_articulo VALUES ('00000000-0000-0000-0000-0000000000d1', 2, 'NO-EXISTE', 1)"),
    # Observacion 6: lineas agregadas a mano
    debe_fallar("6a linea AGREGADA_MANUAL sin agregada_por",
                "INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, motivo_agregada) "
                "VALUES ('00000000-0000-0000-0000-0000000000f1', 1, 1, 'CTR', 100, 'CLP', 100, 1, 'AGREGADA_MANUAL', 'pedido por correo')"),
    debe_fallar("6b linea AGREGADA_MANUAL sin motivo",
                "INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por) "
                "VALUES ('00000000-0000-0000-0000-0000000000f1', 1, 1, 'CTR', 100, 'CLP', 100, 1, 'AGREGADA_MANUAL', 'usuaria')"),
    debe_pasar("6c linea AGREGADA_MANUAL completa",
               "INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, origen, agregada_por, motivo_agregada) "
               "VALUES ('00000000-0000-0000-0000-0000000000f1', 1, 1, 'CTR', 100, 'CLP', 100, 1, 'AGREGADA_MANUAL', 'usuaria', 'pedido por correo')"),
    debe_fallar("6d linea CALCULADA con agregada_por",
                "INSERT INTO linea_ofa (ofa_id, tipo_servicio_id, cantidad, unidad, tarifa, moneda, monto, version_calculo, agregada_por) "
                "VALUES ('00000000-0000-0000-0000-0000000000f1', 1, 1, 'CTR', 100, 'CLP', 100, 1, 'usuaria')"),
    # Observacion 8: decisiones con motivo
    debe_fallar("8a pendiente resuelto como DECISION sin motivo",
                "INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision) "
                "VALUES ('00000000-0000-0000-0000-0000000000a1', 'VARIOS_CONVENIOS_VIGENTES', true, now(), 'usuaria', 'DECISION', '{}')"),
    debe_fallar("8b pendiente DECISION con motivo en blanco",
                "INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision, motivo) "
                "VALUES ('00000000-0000-0000-0000-0000000000a1', 'VARIOS_CONVENIOS_VIGENTES', true, now(), 'usuaria', 'DECISION', '{}', '   ')"),
    debe_fallar("8c resolucion sin usuario ni fecha",
                "INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resolucion) "
                "VALUES ('00000000-0000-0000-0000-0000000000a1', 'TIPO_RETIRO', true, 'DATO_COMPLETADO')"),
    debe_pasar("8d pendiente DECISION completo",
               "INSERT INTO operacion_pendiente (operacion_id, tipo, bloquea_validacion, resuelto_en, resuelto_por, resolucion, decision, motivo) "
               "VALUES ('00000000-0000-0000-0000-0000000000a1', 'VARIOS_CONVENIOS_VIGENTES', true, now(), 'usuaria', 'DECISION', '{\"convenio\":1}', 'Vale el convenio mas antiguo')"),
    debe_fallar("8e omision DESCARTADA sin motivo",
                "INSERT INTO omision_detectada (operacion_id, descripcion, origen, decision, decidida_por, decidida_en) "
                "VALUES ('00000000-0000-0000-0000-0000000000a1', 'Reefer', 'CRUCE_DETERMINISTICO', 'DESCARTADA', 'usuaria', now())"),
    # Observacion 10: alertas sin entidad_id
    debe_pasar("10a alerta abierta sin entidad_id", "INSERT INTO alerta (tipo, entidad) VALUES ('CATALOGO_DESACTUALIZADO', 'catalogo')"),
    debe_fallar("10b segunda alerta abierta igual sin entidad_id", "INSERT INTO alerta (tipo, entidad) VALUES ('CATALOGO_DESACTUALIZADO', 'catalogo')"),
    debe_pasar("10c alerta igual una vez cerrada la anterior",
               "UPDATE alerta SET estado = 'ATENDIDA' WHERE tipo = 'CATALOGO_DESACTUALIZADO'; "
               "INSERT INTO alerta (tipo, entidad) VALUES ('CATALOGO_DESACTUALIZADO', 'catalogo')"),
])
TOTAL_PRUEBAS = PRUEBAS.count("RAISE NOTICE 'OK ")


def main():
    texto = open(MD, encoding="utf-8").read()
    ddl = "\n".join(re.findall(r"```sql\n(.*?)```", texto, flags=re.S))
    datos = tempfile.mkdtemp(prefix="pg_ddl_")
    cluster = os.path.join(datos, "data")
    archivo = os.path.join(datos, "ddl.sql")
    open(archivo, "w", encoding="utf-8").write(
        "\\set ON_ERROR_STOP on\nCREATE EXTENSION IF NOT EXISTS pgcrypto;\n" + ddl + "\n-- PRUEBAS\n" + PRUEBAS)
    ok_pruebas, exito = 0, False
    try:
        r = correr([binario("initdb"), "-D", cluster, "-U", "prueba", "-A", "trust", "-E", "UTF8"])
        print("initdb:", r.returncode, flush=True)
        # Sin capturar la salida: en Windows el servidor hereda los pipes y subprocess quedaria esperando.
        r = subprocess.run([binario("pg_ctl"), "-D", cluster, "-o", f"-p {PUERTO} -c listen_addresses=localhost",
                            "-l", os.path.join(datos, "log.txt"), "-w", "start"],
                           stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        print("start:", r.returncode, flush=True)
        time.sleep(2)
        r = correr([binario("psql"), "-h", "localhost", "-p", PUERTO, "-U", "prueba", "-d", "postgres", "-q", "-f", archivo])
        print("ddl y pruebas:", "OK" if r.returncode == 0 else "ERROR", flush=True)
        for linea in r.stderr.splitlines():
            if "NOTICE:" in linea or "ERROR:" in linea:
                texto_linea = linea.split("NOTICE:")[-1].strip()
                ok_pruebas += texto_linea.startswith("OK ")
                print("  " + texto_linea[:160], flush=True)
        if r.returncode != 0:
            print(r.stderr.strip()[-1500:], flush=True)
        t = correr([binario("psql"), "-h", "localhost", "-p", PUERTO, "-U", "prueba", "-d", "postgres",
                    "-At", "-c", "select count(*) from information_schema.tables where table_schema='public'"])
        print("tablas creadas:", t.stdout.strip(), flush=True)
        exito = r.returncode == 0 and ok_pruebas == TOTAL_PRUEBAS
        print(f"pruebas OK: {ok_pruebas} de {TOTAL_PRUEBAS}", flush=True)
    finally:
        subprocess.run([binario("pg_ctl"), "-D", cluster, "-m", "fast", "-w", "stop"],
                       stdin=subprocess.DEVNULL, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        shutil.rmtree(datos, ignore_errors=True)
        print("cluster temporal borrado", flush=True)
    sys.exit(0 if exito else 1)


if __name__ == "__main__":
    main()
