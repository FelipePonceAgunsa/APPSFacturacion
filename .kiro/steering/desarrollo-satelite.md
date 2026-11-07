# Satélite de Facturación AEXSA — reglas para quien desarrolla

Estas reglas se aplican en todo el trabajo de desarrollo. La guía completa está en `docs/desarrollo/GUIA-DESARROLLADOR.md`.

## Qué es el proyecto
Un sistema nuevo (el Satélite) que recibe el evento facturable de AEXSA SYS, resuelve el convenio vigente, calcula el monto con reglas determinísticas, deja que el facturador revise y ajuste, y arma la prefactura (OFA) para SAP Business One. Fase A: la prefactura se digita en SAP como hoy. Fase B: escritura automática en SAP, folio, pagos y retorno a AEXSA SYS.

## Dónde mirar antes de escribir código
- Qué construir y en qué orden: `.kiro/specs/satelite-facturacion/tasks.md`, sección "Orden de ejecución recomendado".
- Cómo está diseñado: `.kiro/specs/satelite-facturacion/design.md` y `docs/arquitectura/adr/ADR-000-indice.md`.
- Qué pide el negocio: `docs/desarrollo/requerimientos.md`. Reglas citadas como `RN-xxx`: `docs/desarrollo/reglas-negocio.md`. Vocabulario: `docs/desarrollo/glosario.md`.
- Contrato de la API: `docs/arquitectura/api/openapi.yaml`. Modelo de datos: `docs/arquitectura/modelo-datos.md`.
- Cálculo y casos de prueba T1 a T12: `docs/arquitectura/motor-tarifas-detalle.md`.

## Reglas que no se negocian
1. La IA nunca fija un monto. El cálculo es determinístico y versionado. La IA es una etapa opcional y solo propone con confirmación humana.
2. El motor de tarifas es reproducible: mismo `ContextoCalculo`, mismo resultado. Sin reloj, azar, cultura/locale ni consultas en vivo dentro del cálculo. Usar `decimal`, nunca `double`. El tipo de cambio entra como dato.
3. Cada monto se puede reconstruir: toda línea de la OFA guarda su memoria de cálculo. Las tablas de memoria y auditoría son append-only.
4. Convenios, tarifas, catálogo y reglas llevan vigencia y versión. Un cálculo guarda la versión que usó.
5. Todo tercero (SAP, AEXSA SYS, Agunsa Connect, servicio de IA) va detrás de un puerto, con un simulador para construir y probar sin él. El dominio no depende de infraestructura.
6. La integración con SAP usa Outbox: idempotencia, reintentos configurables, registro de solicitud, respuesta y error, validación antes de transmitir, aviso ante fallo y reenvío desde la interfaz sin tocar la base. Nunca llamadas síncronas ni "dispara y olvida". Los módulos se comunican con eventos de dominio.
7. Sin secretos ni contraseñas en el código ni en la configuración: usar AWS Secrets Manager. La autorización por rol se aplica en la API.
8. Nunca datos reales de clientes ni de SAP en el repositorio, en pruebas ni en ejemplos. Los datos de prueba son inventados.
9. Fechas `AAAA-MM-DD`. Los montos llevan moneda explícita (CLP, USD, UF).
10. Si un endpoint cambia, se actualiza `openapi.yaml` en el mismo cambio.

## Stack y convenciones
- .NET 10 (LTS) con Entity Framework Core, API REST, PostgreSQL, Angular. AWS: ECS Fargate, RDS, S3, CloudFront y WAF. CI/CD con GitHub Actions y ECR.
- Arquitectura hexagonal. Proyectos: `Satelite.Dominio`, `Satelite.Aplicacion`, `Satelite.Infraestructura`, `Satelite.Api`, `Satelite.Worker`, `Satelite.Tests`. El dominio no tiene dependencias.
- Nombres del dominio en español, como en el modelo de datos (`Operacion`, `LineaOfa`, `catalogo_snapshot`, `version_convenio`).
- Pruebas: xUnit y FluentAssertions; FsCheck para propiedades; Testcontainers y WebApplicationFactory para integración. Los casos T1 a T12 del motor se escriben antes del código.
- Diagramas en Mermaid.

## Cómo trabajar
- Ejecutar las tareas en el orden recomendado, no por número.
- Al terminar una tarea: marcar `[x]` en `tasks.md` y completar el registro de avance (archivos generados, pruebas, hallazgos, pendientes).
- No inventar. Si falta un dato, marcarlo `[SUPUESTO: razón]` o `[POR CONFIRMAR]` y avisar a TI del proyecto.
- Ante contradicción entre documentos, gana el más reciente; en reglas de negocio, el número mayor.
- Dependencias nuevas: versión exacta fijada y paquete conocido y mantenido.
