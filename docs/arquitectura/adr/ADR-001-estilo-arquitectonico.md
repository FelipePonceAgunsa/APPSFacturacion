# ADR-001 — Estilo arquitectónico y stack tecnológico

Estado: **Aceptado**
Fecha: 2026-10-26
Decide: Arquitecto de Solución

## Contexto
El Satélite es un sistema nuevo que se integra con AEXSA SYS (legacy .NET/SQL Server) y SAP Business One, pasando por el middleware corporativo Agunsa Connect (RNF-05). El estándar técnico corporativo de Agunsa fija el stack: .NET 10 (LTS) + EF Core, API REST, PostgreSQL, Angular en frontend, infraestructura AWS (ECS Fargate, RDS, S3, CloudFront, WAF), CI/CD con GitHub Actions + ECR. Los principios del proyecto exigen cálculo determinístico, versionado y auditable, y cada integración con contrato/idempotencia/reintentos/errores/reproceso/auditoría.

El dominio tiene una regla transversal fuerte: **el monto nunca lo fija la IA**; el cálculo es determinístico y debe reconstruirse (RNF-01, RNF-03). Esto empuja a aislar el dominio de cálculo de la infraestructura y de los adaptadores de integración (que son el mayor riesgo y lo más volátil, PA-001/PA-002).

## Decisión
Adoptar una **arquitectura hexagonal (puertos y adaptadores) con separación por capas y organización por dominios**, desplegada como un **servicio modular (modular monolith)** en la 1ª etapa, no como microservicios.

- **Dominio** (núcleo): entidades, motor de tarifas, reglas, invariantes. Sin dependencias de infraestructura. Determinístico y cubierto por pruebas.
- **Aplicación**: casos de uso que orquestan el flujo de los 7 pasos; definen **puertos** (interfaces) hacia SAP, AEXSA SYS e IA.
- **Infraestructura / adaptadores**: EF Core + PostgreSQL, cliente de Agunsa Connect, cliente de lectura SAP, adaptador de IA, Outbox. Implementan los puertos.
- **API REST**: para el frontend Angular (comercial, facturación, recaudación) y para recibir callbacks.

Stack: **.NET 10 + EF Core + PostgreSQL + REST + Angular**, en **AWS ECS Fargate + RDS PostgreSQL + S3** (documentos de convenio/DTE), **CloudFront + WAF** al frontend, **GitHub Actions + ECR** para CI/CD.

Comunicación entre módulos internos vía **Domain Events** in-process (estándar corporativo), con posibilidad de externalizarlos a una cola si se separa un módulo en el futuro.

## Alternativas consideradas
1. **Microservicios desde el inicio.** Descartado para la 1ª etapa: el volumen (~2.428 documentos/mes, RNF-04) no lo exige, y fragmentar la integración SAP —aún no resuelta (PA-001)— añadiría riesgo operacional sin beneficio. El monolito modular permite extraer un módulo después sin reescribir el dominio.
2. **Arquitectura en capas tradicional (sin puertos).** Descartada: acoplaría el dominio determinístico a los adaptadores de SAP/AEXSA, que son lo más incierto. La hexagonal permite construir y probar el cálculo sin esperar a que se resuelva la escritura en SAP.
3. **Stack distinto (p. ej. Java o Node).** Descartado: contradice el estándar corporativo y la base de código/experiencia del grupo.

## Consecuencias
- El motor de tarifas y las reglas se construyen y prueban **antes** de resolver la integración SAP (desacople de riesgo).
- Los adaptadores de SAP/AEXSA/IA son reemplazables: si el proveedor externo cambia el mecanismo de escritura (PA-001), solo cambia el adaptador, no el dominio.
- Un solo despliegue simplifica operación y auditoría transaccional (la trazabilidad RNF-02 vive en una sola base).
- Se asume costo de disciplina: mantener el dominio libre de dependencias de infraestructura.

## Reglas / requerimientos relacionados
RNF-03 (determinismo), RNF-05 (integración gobernada), RNF-06 (no disrupción), principios de arquitectura (steering).
