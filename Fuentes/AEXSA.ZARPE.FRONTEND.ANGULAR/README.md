# AEXSA.ZARPE.FRONTEND.ANGULAR

Frontend de **ZARPE - FACTURACIÓN**. Sigue el estándar AGUNSA (plantilla `AGUNSA.BPT.FRONTEND.ANGULAR`): Angular 22 standalone y zoneless, Material 3 con el tema corporativo, cliente generado con OpenAPI Generator, side-menu, control de acceso en tres capas y aviso de inactividad.

La diferencia es el inicio de sesión. Se hace con **Cognito federado con Entra ID** (ADR-016), con Authorization Code + PKCE (`angular-auth-oidc-client`). La app no tiene formulario de usuario y contraseña, recuperación de contraseña, perfil, selector de empresa ni mantenedores de usuarios y roles.

## Requisitos

- Node.js 24
- Java 11 o superior, solo para regenerar el cliente de la API

## Ejecutar en local

```powershell
npm install
npm start
```

La app queda en `http://localhost:4200`. El backend debe correr en `http://localhost:5228`.

> El `.npmrc` trae `legacy-peer-deps=true` porque ngx-toastr 20.0.5 todavía declara peer `@angular/common ^21`. Quitarlo cuando publique una versión compatible con Angular 22.

## Configuración de ambiente

`src/environments/environment.development.ts` (local) y `src/environments/environment.ts` (lo que hornea `ng build`):

| Clave | Valor |
|-------|-------|
| `apiUrl` | URL del backend |
| `cognito.authority` | `https://cognito-idp.<region>.amazonaws.com/<userPoolId>` `[POR CONFIRMAR PA-009]` |
| `cognito.clientId` | App client del Satélite. Debe ser el mismo que `Auth:ClientId` del backend |
| `cognito.domain` | Dominio del Hosted UI, para cerrar sesión: `https://<dominio>.auth.<region>.amazoncognito.com` |
| `cognito.scope` | `openid email profile` |

En el app client de Cognito hay que registrar `http://localhost:4200` (y la URL de cada ambiente) como callback URL y sign-out URL.

Mientras `authority` o `clientId` estén vacíos, la pantalla de ingreso avisa que el inicio de sesión no está configurado.

## Flujo de sesión

1. "Ingresar con cuenta AGUNSA" redirige a Cognito, que delega en Entra ID.
2. Al volver, `AuthService.init()` (app initializer) procesa el código y llama a `GET /api/Session/Me`.
3. La API responde con los roles y accesos del Satélite, que alimentan el menú, las rutas y `*hasAccess`. Si el usuario no tiene rol del Satélite, la API responde 403 y la pantalla de ingreso lo informa.
4. `authInterceptor()` adjunta el access token solo a las llamadas a `apiUrl`. La librería renueva el token con el refresh token.
5. Tras 15 minutos de inactividad aparece el aviso y luego se cierra la sesión, también en Cognito.

## Sistema de accesos

| Capa | Dónde | Qué hace |
|------|-------|----------|
| Rutas | `app.routes.ts` + `authGuard` | Exige sesión y `data.access`; si no, redirige a `/` o `/forbidden` |
| Menú lateral | `helpers/menu.constants.ts` | Oculta ítems sin permiso; también define el home |
| Elementos HTML | Directiva `*hasAccess` | Muestra u oculta elementos |

La API vuelve a validar cada request; el frontend solo oculta.

Para agregar un módulo:

1. Constante en `helpers/access.constants.ts`, con el mismo nombre que `AccessNames.cs` del backend.
2. Ruta: `{ path: 'convenios', component: ConveniosComponent, canActivate: [authGuard], data: { access: [Access.Convenios] } }`.
3. Ítem de menú: `{ label: 'Convenios', icon: 'handshake', route: '/convenios', access: [Access.Convenios] }`.
4. En plantillas: `<button *hasAccess="[Access.Convenios_Edit]">Editar</button>`.

## Cliente de la API

Con el backend corriendo:

```powershell
npm run api:generate
```

Borra y regenera `src/app/api/` desde `http://localhost:5228/openapi/v1.json`. Los archivos generados no se editan a mano.

## Pruebas

```powershell
npm test
```

## Estructura

```
src/app/
├── api/                    # Cliente generado (no editar)
├── components/
│   ├── dialog-session-warning/  # Aviso de inactividad
│   ├── forbidden/          # Página 403
│   ├── inicio/             # Pantalla de inicio
│   ├── login/              # Ingreso con cuenta corporativa
│   └── side-menu/          # Layout con menú lateral
├── helpers/
│   ├── access.constants.ts # Constantes de permisos
│   ├── auth.guard.ts       # Guard de rutas
│   ├── date-interceptor.ts # Fechas ISO → Date
│   ├── has-access.directive.ts
│   ├── menu.constants.ts   # Menú y home
│   └── oidc.config.ts      # Configuración de Cognito
├── services/
│   ├── app-version.service.ts
│   ├── auth.service.ts     # Sesión, usuario, accesos
│   ├── jwt-interceptor.service.ts  # 401 → cierre de sesión, mensajes de error
│   └── session-manager.service.ts  # Inactividad
├── shared/                 # Paginador en español
├── app.routes.ts
└── app.config.ts
```

## Imagen Docker y versión desplegada

Antes de construir, fijar `apiUrl` y `cognito` en `src/environments/environment.ts`.

```bash
TAG=qa-$(date +%y%m%d)-$(date +%H%M)
docker build --build-arg IMAGE_TAG=$TAG -t <registry>/zarpe-frontend:$TAG .
curl https://<front>/assets/version.json
```

## Stack

- Angular 22.2 / TypeScript 6.0
- Angular Material 22.2
- angular-auth-oidc-client 22.0.2
- Luxon, ngx-toastr
- OpenAPI Generator
- Vitest 4
