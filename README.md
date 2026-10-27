# Satélite de Facturación AEXSA — Arquitectura

Este repositorio versiona **únicamente los artefactos de arquitectura** del Satélite de Facturación AEXSA.

## Contenido

```
docs/arquitectura/
├── adr/                        Decisiones de arquitectura (ADR-000…011)
├── preguntas-arquitectura.md   Decisiones técnicas que dependen de terceros (PA-001…008)
├── plan-entregas.md            Priorización por valor: entregas E1–E5 (Fase A/B)
├── estimacion-hh.md            Esfuerzo en horas de desarrollo dirigido con Kiro
└── estado-arquitectura.md      Punto de control del entregable

.kiro/specs/satelite-facturacion/
├── design.md                   Diseño de arquitectura (capas, dominio, flujo, diagramas)
└── tasks.md                    Plan de implementación por fases/entregas
```

## Qué NO está en este repo (por diseño)
- Levantamiento de negocio, procesos, reglas, brechas y entrevistas.
- Insumos de convenios (documentos con datos de clientes — ley de datos personales, BR-021).
- Presentaciones y `requirements.md` de negocio.
- Configuración de agentes/steering de Kiro.

Esos artefactos viven fuera del control de versiones de este repositorio.

## Punto de partida
Leer en orden: `docs/arquitectura/adr/ADR-000-indice.md` → `design.md` → `plan-entregas.md`.
