# ADR-016 — Autenticación y autorización de usuarios: Cognito federado con Entra ID

Estado: **Aceptado** (definición corporativa) · detalles de configuración en **Propuesto** (PA-009)
Fecha: 2026-10-28
Decide: Arquitecto de Solución
Origen: definición de la empresa, confirmada por Felipe Ponce (TI): la identidad corporativa se resuelve con **Amazon Cognito y Microsoft Entra ID**.

## Contexto
`api/openapi.yaml` ya declaraba un token Bearer JWT para el frontend, pero no decía quién lo emite. `restricciones-tecnicas.md` tenía la autenticación como `[POR CONFIRMAR]`. El F02 exige control de acceso por rol (Comercial, Facturación, Recaudación) y auditoría de quién hace cada ajuste o confirmación (REQ-006, REQ-011). El legacy guardaba credenciales en claro (BR-016), así que el Satélite no debe manejar contraseñas propias.

## Decisión
1. **Entra ID es el proveedor de identidad.** Mantiene el directorio de personas, las credenciales, el MFA y las altas y bajas.
2. **Cognito es el intermediario.** Un user pool federado con Entra ID emite los tokens que consume el Satélite.
3. **Frontend Angular:** flujo Authorization Code con PKCE contra Cognito. No maneja contraseñas.
4. **API (.NET):** solo valida el JWT de Cognito: firma (JWKS), emisor, audiencia (client id) y expiración. No emite tokens ni guarda credenciales.
5. **Roles:** los grupos o app roles de Entra se traducen a los roles del Satélite (Comercial, Facturación, Recaudación, Administrador). La autorización se aplica **en la API**, no solo ocultando menús en el frontend. Las acciones sensibles requieren rol explícito: confirmar convenio, ajuste manual, promover regla, reenviar OFA.
6. **Auditoría:** la identidad del token (`sub` y correo) se registra como `usuario` en `evento_auditoria` y en el ajuste manual.
7. **Callbacks de integración** (Agunsa Connect → Satélite) no usan este flujo. Son máquina a máquina y su mecanismo depende de PA-002. Si Agunsa Connect lo soporta, se puede usar un cliente de Cognito con client credentials; hasta entonces queda la firma por header del OpenAPI como supuesto.
8. Secretos del cliente de Cognito, si aplican, van en Secrets Manager.

## Alternativas consideradas
1. **Usuarios y contraseñas propias en la base del Satélite.** Descartada: duplica el directorio, las bajas serían manuales y repite el antipatrón del legacy.
2. **Entra ID directo, sin Cognito.** Descartada: la definición de la empresa pone a Cognito como capa de acceso.
3. **API keys por usuario.** Descartada: no identifican a la persona ni heredan MFA.

## Consecuencias
- Sin contraseñas en el Satélite; la baja de una persona en Entra corta su acceso.
- La API queda atada a Cognito como emisor. Un cambio de proveedor solo toca la validación del token.
- Riesgo a vigilar: si los roles se leen de grupos de Entra, un usuario con muchos grupos puede exceder el límite del token y perder el claim. Usar app roles de Entra evita el problema. Se decide en PA-009.
- El mockup no tiene pantalla de login (entra directo con un usuario de ejemplo por área). Es una simplificación del prototipo, no del diseño.

## Por confirmar (PA-009)
- Si se usa un user pool de Cognito existente de la empresa o uno nuevo para el Satélite.
- Federación por OIDC o por SAML (`[SUPUESTO: OIDC]`).
- Si los roles salen de grupos o de app roles de Entra, y cómo se llaman.
- Matriz de permisos por rol y duración de los tokens.

## Reglas / requerimientos relacionados
REQ-006, REQ-011, RNF-05, BR-016, BR-021, PA-002, PA-005, PA-009. Complementa ADR-001 y `arquitectura-despliegue.md` §3.
