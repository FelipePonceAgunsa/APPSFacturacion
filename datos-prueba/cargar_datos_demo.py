"""Carga los datos de prueba (inventados) de datos-prueba/ en la API local de ZARPE - FACTURACIÓN.

Sirve para entrar a usar el software con datos: catálogo SAP de ejemplo, vínculos de servicios,
dos convenios vigentes, eventos de AEXSA y la OFA calculada de cada operación.

Uso (con la API corriendo en Visual Studio, F5):
    python datos-prueba/cargar_datos_demo.py
    python datos-prueba/cargar_datos_demo.py --api http://localhost:5228

Se puede correr varias veces: lo que ya está cargado se salta (catálogo, convenios y eventos repetidos).
Solo usa la biblioteca estándar de Python (sin pip install). No requiere PowerShell.
Todos los datos son inventados: ningún cliente ni dato de SAP real.
"""

import argparse
import base64
import json
import os
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

RAIZ = Path(__file__).resolve().parent

# Servicio del catálogo normalizado -> artículo SAP de ejemplo (A1_articulos_20261101.csv).
VINCULOS = {
    42: "SRV-REC-001", 43: "SRV-DES-001", 44: "SRV-MOV-001", 45: "SRV-VIS-001",
    46: "SRV-SEC-001", 47: "SRV-REF-001", 48: "SRV-ALM-001", 49: "SRV-SAG-001",
    50: "SRV-USO-001", 51: "SRV-DOC-001", 52: "SRV-MAN-001",
}

# 07 y 08 se omiten: son inválidos a propósito (la API responde 400) y no dejan nada que usar.
EVENTOS = [
    "01-impo-indirecto-con-convenio.json",
    "02-directo-sti-sin-convenio.json",
    "03-directo-dpworld-sin-convenio.json",
    "04-expo-sin-cantidad-dus.json",
    "05-tipo-retiro-null.json",
    "06-version-2-de-01.json",
    "09-bulto.json",
    "10-comparte-bl-con-01.json",
]


class Api:
    def __init__(self, base: str):
        self.base = base.rstrip("/")

    def _enviar(self, metodo: str, ruta: str, cuerpo: bytes | None, tipo: str | None,
                cabeceras: dict[str, str] | None = None):
        req = urllib.request.Request(self.base + ruta, data=cuerpo, method=metodo)
        req.add_header("Accept", "application/json")
        for nombre, valor in (cabeceras or {}).items():
            req.add_header(nombre, valor)
        if tipo:
            req.add_header("Content-Type", tipo)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, _leer_json(r.read())
        except urllib.error.HTTPError as e:
            return e.code, _leer_json(e.read())

    def json(self, metodo: str, ruta: str, cuerpo=None, cabeceras: dict[str, str] | None = None):
        datos = None
        if cuerpo is not None:
            texto = cuerpo if isinstance(cuerpo, str) else json.dumps(cuerpo, ensure_ascii=False)
            datos = texto.encode("utf-8")
        return self._enviar(metodo, ruta, datos, "application/json; charset=utf-8", cabeceras)

    def multipart(self, ruta: str, campos: dict[str, str], archivos: dict[str, Path]):
        limite = "----zarpe" + uuid.uuid4().hex
        partes = []
        for nombre, valor in campos.items():
            partes.append(
                f'--{limite}\r\nContent-Disposition: form-data; name="{nombre}"\r\n\r\n{valor}\r\n'.encode("utf-8"))
        for nombre, ruta_archivo in archivos.items():
            cabecera = (f'--{limite}\r\nContent-Disposition: form-data; name="{nombre}"; '
                        f'filename="{ruta_archivo.name}"\r\nContent-Type: text/csv\r\n\r\n')
            partes.append(cabecera.encode("utf-8") + ruta_archivo.read_bytes() + b"\r\n")
        partes.append(f"--{limite}--\r\n".encode("utf-8"))
        return self._enviar("POST", ruta, b"".join(partes), f"multipart/form-data; boundary={limite}")


def _leer_json(datos: bytes):
    if not datos:
        return None
    try:
        return json.loads(datos.decode("utf-8"))
    except ValueError:
        return datos.decode("utf-8", errors="replace")


class Falla(Exception):
    pass


def exigir(codigo: int, cuerpo, esperados: tuple[int, ...], que: str):
    if codigo not in esperados:
        raise Falla(f"{que}: HTTP {codigo} {json.dumps(cuerpo, ensure_ascii=False) if cuerpo else ''}")


def paso_sesion(api: Api):
    codigo, yo = api.json("GET", "/api/Session/Me")
    exigir(codigo, yo, (200,), "GET /api/Session/Me")
    roles = (yo or {}).get("roles", [])
    print(f"   usuario {yo.get('userName') or yo.get('sub') or '?'} · roles: {', '.join(roles)}")
    if "Administrador" not in roles:
        raise Falla("la API no está en modo abierto como Administrador (revisa que corra en Development)")


def paso_catalogo(api: Api):
    codigo, resumen = api.json("GET", "/api/catalogo")
    if codigo == 200 and resumen:
        print(f"   ya hay catálogo vigente (versión {resumen.get('version')}): se salta la importación")
        return
    c = RAIZ / "catalogo"
    codigo, r = api.multipart("/api/catalogo/importaciones", {"tipo": "COMPLETO"}, {
        "articulos": c / "A1_articulos_20261101.csv",
        "listasPrecios": c / "A2_listas_precios_20261101.csv",
        "kits": c / "A3_kits_20261101.csv",
        "tipoCambio": c / "A4_tipo_cambio_20261101.csv",
    })
    exigir(codigo, r, (200, 201), "importar catálogo")
    if r.get("estado") != "PENDIENTE_CONFIRMACION":
        raise Falla(f"importación en estado {r.get('estado')}: {json.dumps(r.get('errores'), ensure_ascii=False)}")
    codigo, res = api.json("POST", f"/api/catalogo/importaciones/{r['importacionId']}/confirmar")
    exigir(codigo, res, (200,), "confirmar catálogo")
    print(f"   catálogo versión {res.get('version')}: {res.get('articulos')} artículos, "
          f"{res.get('listasPrecios')} listas, {res.get('kits')} kits, TC al {res.get('tipoCambioAl')}")


def paso_vinculos(api: Api):
    codigo, servicios = api.json("GET", "/api/tipos-servicio")
    exigir(codigo, servicios, (200,), "listar servicios")
    existentes = {s.get("tipoServicioId") for s in servicios or []}
    faltan = sorted(set(VINCULOS) - existentes)
    if faltan:
        raise Falla(f"faltan los servicios {faltan} en la base. Carga las semillas de db/semillas/*.sql "
                    "(las aplica db/crear-base-local.bat) y vuelve a correr este script.")
    for servicio, articulo in VINCULOS.items():
        codigo, v = api.json("PUT", f"/api/catalogo/vinculos/{servicio}", {"articuloSap": articulo})
        exigir(codigo, v, (200,), f"vincular servicio {servicio}")
    print(f"   {len(VINCULOS)} servicios vinculados a su artículo SAP")


def paso_convenios(api: Api):
    for archivo in sorted((RAIZ / "convenios").glob("convenio-*.json")):
        texto = archivo.read_text(encoding="utf-8")
        rut = json.loads(texto)["rut"]
        codigo, lista = api.json("GET", f"/api/convenios?rut={urllib.request.quote(rut)}")
        exigir(codigo, lista, (200,), f"buscar convenio {rut}")
        if lista:
            print(f"   {rut}: ya existe, se salta")
            continue
        codigo, borrador = api.json("POST", "/api/convenios", texto)
        exigir(codigo, borrador, (200, 201), f"crear convenio {rut}")
        codigo, conf = api.json(
            "POST", f"/api/convenios/{borrador['convenioId']}/versiones/{borrador['versionId']}/confirmar")
        exigir(codigo, conf, (200,), f"confirmar convenio {rut}")
        print(f"   {rut}: creado y confirmado ({conf.get('estado', 'VIGENTE')})")


def paso_eventos(api: Api):
    # POST /api/aexsa/eventos exige HTTP Basic (credencial de AEXSA SYS, sección Aexsa:Basic de la API).
    # La misma credencial que pusiste en user-secrets se entrega por variables de entorno, nunca en el código.
    usuario = os.environ.get("ZARPE_AEXSA_USUARIO", "")
    clave = os.environ.get("ZARPE_AEXSA_CLAVE", "")
    cabeceras = {}
    if usuario and clave:
        cabeceras["Authorization"] = "Basic " + base64.b64encode(f"{usuario}:{clave}".encode("utf-8")).decode("ascii")
    for nombre in EVENTOS:
        texto = (RAIZ / "eventos" / nombre).read_text(encoding="utf-8")
        codigo, r = api.json("POST", "/api/aexsa/eventos", texto, cabeceras)
        if codigo == 401:
            raise Falla(
                "la API pide la credencial de AEXSA (HTTP 401). Configúrala en user-secrets de Satelite.Api "
                "(Aexsa:Basic:Usuario y Aexsa:Basic:Clave), reinicia la API y define las mismas en "
                "ZARPE_AEXSA_USUARIO y ZARPE_AEXSA_CLAVE antes de correr este script (ver datos-prueba/README.md).")
        if codigo in (200, 201):
            print(f"   {nombre}: {r.get('resultado')} · operación {r.get('operacionId')}")
        elif codigo == 409 and isinstance(r, dict) and r.get("codigo") == "YA_RECIBIDO":
            print(f"   {nombre}: ya recibido antes, se salta")
        else:
            # No detiene la carga: se informa y se sigue con el próximo evento.
            print(f"   {nombre}: HTTP {codigo} {json.dumps(r, ensure_ascii=False) if r else ''}")


def paso_ofas(api: Api):
    codigo, operaciones = api.json("GET", "/api/operaciones")
    exigir(codigo, operaciones, (200,), "listar operaciones")
    calculadas = 0
    for op in operaciones or []:
        op_id = op.get("operacionId")
        codigo, ofa = api.json("POST", f"/api/operaciones/{op_id}/ofa")
        if codigo == 200:
            calculadas += 1
            print(f"   operación {op_id}: OFA {ofa.get('estado')} · {len(ofa.get('lineas', []))} líneas · "
                  f"total {ofa.get('totalClp')} CLP")
        else:
            codigo_error = ofa.get("codigo") if isinstance(ofa, dict) else ""
            print(f"   operación {op_id}: no se calculó (HTTP {codigo} {codigo_error}). "
                  "Revísala en la pantalla: puede tener datos por completar.")
    print(f"   {calculadas} de {len(operaciones or [])} operaciones con OFA calculada")


def main() -> int:
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument("--api", default="http://localhost:5228", help="URL de la API local")
    args = p.parse_args()
    api = Api(args.api)

    pasos = [
        ("1. La API responde (modo abierto, Administrador)", paso_sesion),
        ("2. Catálogo SAP de ejemplo (A1 a A4)", paso_catalogo),
        ("3. Vincular servicios 42 a 52 con su artículo SAP", paso_vinculos),
        ("4. Convenios de ejemplo (76.111.111-6 y 77.222.222-K)", paso_convenios),
        ("5. Eventos de AEXSA de ejemplo", paso_eventos),
        ("6. Calcular la OFA de cada operación", paso_ofas),
    ]
    for titulo, paso in pasos:
        print(f"== {titulo}")
        try:
            paso(api)
        except urllib.error.URLError as e:
            print(f"FALLA: no hay conexión con {args.api} ({e.reason}). ¿Está corriendo la API (F5 en Visual Studio)?")
            return 1
        except Falla as e:
            print(f"FALLA: {e}")
            return 1
    print("\nListo. Abre http://localhost:4200 (frontend) o http://localhost:5228/scalar/v1 (API).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
