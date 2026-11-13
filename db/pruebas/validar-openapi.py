"""Valida que openapi.yaml sea YAML valido, que no tenga referencias rotas y muestra el esquema del evento de AEXSA.
Uso (desde la raiz del repo): python db/pruebas/validar-openapi.py
"""
import re
import sys
from pathlib import Path

import yaml

ruta = Path(__file__).resolve().parents[2] / "docs" / "arquitectura" / "api" / "openapi.yaml"
texto = ruta.read_text(encoding="utf-8")
doc = yaml.safe_load(texto)
esquemas = doc["components"]["schemas"]

rotas = sorted(set(re.findall(r"#/components/schemas/(\w+)", texto)) - set(esquemas))
evento = esquemas["EventoFacturable"]
inspeccion = esquemas["ContenedorOperacion"]["properties"]["inspecciones"]["items"]

print("YAML valido")
print("Referencias rotas:", rotas or "ninguna")
print("EventoFacturable.required:", evento["required"])
print("EventoFacturable.properties:", list(evento["properties"]))
print("ContenedorOperacion.properties:", list(esquemas["ContenedorOperacion"]["properties"]))
print("Inspeccion.properties:", list(inspeccion["properties"]))
faltan = [c for c in evento["required"] if c not in evento["properties"]]
print("Requeridos sin propiedad:", faltan or "ninguno")
sys.exit(1 if rotas or faltan else 0)
