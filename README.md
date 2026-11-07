# Satélite de Facturación AEXSA

Sistema nuevo que recibe el evento facturable de AEXSA SYS, resuelve el convenio vigente, calcula el monto con reglas determinísticas y arma la prefactura (OFA) para SAP Business One. La IA nunca fija un monto.

Este repositorio contiene **lo necesario para construirlo**: las decisiones de arquitectura, el diseño, el plan de tareas, el contrato de la API y el material de arranque para quien desarrolla.

## Si vas a desarrollar: empieza aquí
1. Lee **`docs/desarrollo/GUIA-DESARROLLADOR.md`** (20 minutos).
2. Abre la carpeta en **Kiro**. Kiro aplica solo las reglas de `.kiro/steering/desarrollo-satelite.md`.
3. Abre **`.kiro/specs/satelite-facturacion/tasks.md`** y sigue el "Orden de ejecución recomendado".

## Contenido

```
docs/desarrollo/
├── GUIA-DESARROLLADOR.md        Cómo arrancar: qué construyes, reglas, orden, Kiro, puntos abiertos
├── requerimientos.md            Qué pide el negocio (REQ-001…012) con los ajustes vigentes
├── reglas-negocio.md            Las 79 reglas (RN-001…079), sin nombres de personas ni de clientes
├── glosario.md                  Vocabulario del negocio
├── formato-archivos-catalogo-sap.md   Columnas de los CSV que exporta SAP (Fase A)
└── diagramas/                   Flujo del sistema y flujo del cálculo (SVG)

docs/arquitectura/
├── adr/                         Decisiones de arquitectura (ADR-000…017)
├── arquitectura-despliegue.md   Dónde y cómo se levanta (AWS, red, CI/CD)
├── modelo-datos.md              Modelo físico de BD (DDL PostgreSQL)
├── contratos-integracion.md     Contratos de los puertos (SAP, AEXSA, IA)
├── motor-tarifas-detalle.md     Pipeline de reglas y casos de prueba T1…T12
├── restricciones-tecnicas.md    Lo confirmado y lo pendiente, enlazado a las PA
├── estrategia-pruebas.md        Cómo se garantiza el determinismo y las integraciones
├── mapa-pantallas.md            Pantallas por rol y su API
└── api/openapi.yaml             Contrato REST frontend↔backend (v1.1.0)

.kiro/
├── specs/satelite-facturacion/  design.md (cómo) y tasks.md (qué y en qué orden)
└── steering/                    desarrollo-satelite.md (reglas para quien desarrolla)
```

## Qué NO está en este repositorio (por diseño)
- Los documentos originales del levantamiento y el `requirements.md` original de negocio. En `docs/desarrollo/` hay extractos sin nombres de personas ni de clientes.
- Insumos de convenios y cualquier dato de clientes (ley de protección de datos personales, BR-021).
- Documentación de gestión y presentaciones (plan de entregas, estimación, estado, solicitudes a SAP).
- El prototipo navegable de la interfaz. Se entrega por canal interno.

## Reglas del repositorio
- Nunca datos reales de clientes ni de SAP: ni en código, ni en pruebas, ni en ejemplos.
- Nunca secretos ni contraseñas (se usa AWS Secrets Manager).
- Si cambia un endpoint, cambia `docs/arquitectura/api/openapi.yaml` en el mismo cambio.
