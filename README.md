# Satélite de Facturación AEXSA — Entregable de arquitectura

Este repositorio versiona **lo entregable para construir el software**: las decisiones de arquitectura (ADR), el diseño y el plan de implementación.

## Contenido

```
docs/arquitectura/
├── adr/                         Decisiones de arquitectura (ADR-000…011)
├── arquitectura-despliegue.md   Dónde y cómo se levanta el aplicativo (AWS, red, CI/CD)
└── modelo-datos.md              Modelo físico de BD (DDL PostgreSQL, tablas, claves, índices)

.kiro/specs/satelite-facturacion/
├── design.md                    Diseño de arquitectura (capas, dominio, flujo, diagramas)
└── tasks.md                     Plan de implementación por fases/entregas
```

## Qué NO está en este repo (por diseño)
- Documentación de gestión y presentación: preguntas de arquitectura, plan de entregas, estimación de esfuerzo y estado del proyecto (se mantienen fuera del control de versiones).
- Levantamiento de negocio, procesos, reglas, brechas y entrevistas.
- Insumos de convenios (documentos con datos de clientes — ley de datos personales, BR-021).
- `requirements.md` de negocio y configuración de agentes/steering de Kiro.

## Punto de partida
Leer en orden: `docs/arquitectura/adr/ADR-000-indice.md` → `.kiro/specs/satelite-facturacion/design.md` → `tasks.md`.
