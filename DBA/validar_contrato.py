# -*- coding: utf-8 -*-
"""Compara el EventoFacturable de contratos-integracion.md §2 (ejemplo jsonc) con el esquema del OpenAPI.

Revisa, en el mensaje y en sus objetos anidados, que los nombres de campo coincidan y que los campos
de prioridad A del contrato esten en `required` del OpenAPI. Imprime las diferencias encontradas y
termina con codigo 1 si hay alguna.

Uso (desde la raiz del repositorio):  python DBA/validar_contrato.py
Requiere: pip install ruamel.yaml
"""
import json, os, re, sys
from ruamel.yaml import YAML

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
CONTRATO = os.path.join(RAIZ, "docs", "arquitectura", "contratos-integracion.md")
OPENAPI = os.path.join(RAIZ, "docs", "arquitectura", "api", "openapi.yaml")

texto = open(CONTRATO, encoding="utf-8").read()
bloque = re.search(r"// EventoFacturableDto.*?\n(\{.*?\n\})\n```", texto, flags=re.S).group(1)

prioridad_a = set()          # campos del mensaje (primer nivel) con prioridad A incondicional
condicionales = set()        # "A en impo", "A en expo": obligatorios segun el sentido (regla, no required)
servicio_a = set()           # campos A dentro de cada servicio
lineas_json = []
for linea in bloque.splitlines():
    m = re.match(r'^(\s*)"(\w+)":.*//\s*A\b(\s*en\b)?', linea)
    if m:
        sangria, campo, condicional = len(m.group(1)), m.group(2), m.group(3)
        if sangria == 2:
            (condicionales if condicional else prioridad_a).add(campo)
        elif campo in ("tipoServicioId", "cantidad"):
            servicio_a.add(campo)
    lineas_json.append(re.sub(r"\s//.*$", "", linea))          # quita comentarios jsonc
evento = json.loads("\n".join(lineas_json))

oa = YAML(typ="safe").load(open(OPENAPI, encoding="utf-8"))
esq = oa["components"]["schemas"]


def props(nombre):
    s = esq[nombre]
    p = dict(s.get("properties", {}))
    for parte in s.get("allOf", []):
        if "$ref" in parte:
            p.update(props(parte["$ref"].split("/")[-1]))
        p.update(parte.get("properties", {}))
    return p


def props_campo(definicion):
    """Propiedades de un campo objeto del OpenAPI, resolviendo $ref y allOf."""
    if "$ref" in definicion:
        return props(definicion["$ref"].split("/")[-1])
    p = dict(definicion.get("properties", {}))
    for parte in definicion.get("allOf", []):
        if "$ref" in parte:
            p.update(props(parte["$ref"].split("/")[-1]))
        p.update(parte.get("properties", {}))
    if "items" in definicion:
        return props_campo(definicion["items"])
    return p


diferencias = []


def comparar(ruta, ejemplo, definidos):
    claves = set(ejemplo)
    for c in sorted(claves - set(definidos)):
        diferencias.append(f"{ruta}.{c}: está en el contrato y no en el OpenAPI")
    for c in sorted(set(definidos) - claves):
        diferencias.append(f"{ruta}.{c}: está en el OpenAPI y no en el contrato")
    for c in sorted(claves & set(definidos)):
        valor = ejemplo[c]
        if isinstance(valor, list) and valor and isinstance(valor[0], dict):
            valor = valor[0]
        if isinstance(valor, dict):
            sub = props_campo(definidos[c])
            if sub:
                comparar(f"{ruta}.{c}", valor, sub)


comparar("EventoFacturable", evento, props("EventoFacturable"))
requeridos = set(esq["EventoFacturable"]["required"])
for c in sorted(prioridad_a - requeridos):
    diferencias.append(f"EventoFacturable.{c}: prioridad A en el contrato pero no está en required del OpenAPI")
for c in sorted(requeridos - prioridad_a):
    diferencias.append(f"EventoFacturable.{c}: required en el OpenAPI pero no marcado A en el contrato")

req_servicio = set(esq["ServicioEvento"].get("required", []))
for c in sorted(servicio_a - req_servicio):
    diferencias.append(f"ServicioEvento.{c}: prioridad A en el contrato pero no está en required del OpenAPI")
desc = esq["EventoFacturable"]["description"]
for c in sorted(condicionales):
    if c not in desc:
        diferencias.append(f"EventoFacturable.{c}: condicional en el contrato pero el OpenAPI no lo documenta")

print("campos del contrato:", len(evento), "· prioridad A:", len(prioridad_a),
      "· condicionales:", sorted(condicionales), "· required OpenAPI:", len(requeridos))
print("diferencias:", len(diferencias))
for d in diferencias:
    print(" -", d)
sys.exit(1 if diferencias else 0)
