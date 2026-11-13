# AEXSA.ZARPE.BACKEND.NET

Backend de **ZARPE - FACTURACIÓN** (el Satélite). Sigue el estándar AGUNSA (plantilla `AGUNSA.BPT.BACKEND.NET`) con dos diferencias que fijan los ADR del proyecto:

- **Arquitectura hexagonal** en una sola solución con varios proyectos (ADR-001).
- **Autenticación con Cognito federado con Entra ID** (ADR-016): la API solo valida el JWT. No hay usuarios, contraseñas, emisión de tokens, recuperación de contraseña ni SMTP.

## Requisitos

- .NET SDK 10 (`global.json` fija la banda 10.0.x)
- Docker Desktop, para la base PostgreSQL local de `../../db`

## Estructura

```
AEXSA.ZARPE.BACKEND.NET/
├── AEXSA.ZARPE.BACKEND.NET.sln
├── Directory.Build.props        # net10.0, Nullable, ImplicitUsings para todos los proyectos
├── global.json                  # SDK 10 y runner de pruebas Microsoft.Testing.Platform
├── Satelite.Dominio/            # Entidades (Entidades/), enums (Comun/), motor de tarifas (Motor/). Sin dependencias
├── Satelite.Aplicacion/         # Casos de uso y puertos → Dominio
│   └── Comun/                   # ServiceResult<T>, ICurrentUser
├── Satelite.Infraestructura/    # EF Core + PostgreSQL, adaptadores → Aplicacion
│   └── Persistencia/            # PrincipalDbContext, Configuraciones/ (mapeo de las 35 tablas), Migraciones/
├── Satelite.Api/                # REST para el frontend → Aplicacion + Infraestructura
│   ├── Controllers/             # SessionController (GET /api/Session/Me)
│   ├── Extensions/              # Cognito, autorización, esquema Bearer de OpenAPI
│   ├── Handlers/                # AccessNames, [AccessAuthorize], roles y matriz de accesos
│   ├── Models/                  # DTOs de la API
│   ├── Services/                # CurrentUser
│   ├── Program.cs
│   └── Dockerfile
└── Satelite.Tests/              # xUnit v3 (pruebas de arquitectura entre capas)
```

Dependencias: Dominio ← Aplicacion ← Infraestructura ← Api. `Satelite.Tests` comprueba que Dominio y Aplicacion no referencian infraestructura.

`Satelite.Worker` (Outbox hacia SAP) no se creó: en la etapa 1 no hay Worker. Se agrega en la etapa 2.

## Ejecutar en local

1. Levantar la base (contenedor `zarpe-postgres`, puerto 5433):

   ```powershell
   cd ..\..\db
   docker compose up -d
   ```

2. Cargar la cadena de conexión en user-secrets. Las contraseñas no van en appsettings (regla 7 del steering):

   ```powershell
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5433;Database=zarpe_facturacion;Username=postgres;Password=postgres" --project Satelite.Api
   ```

3. Ejecutar:

   ```powershell
   dotnet run --project Satelite.Api
   ```

| URL | Qué es |
|-----|--------|
| `http://localhost:5228/` | Estado y versión (`IMAGE_TAG`) |
| `http://localhost:5228/health` | Liveness, para el health check del ALB. No consulta la base |
| `http://localhost:5228/health/db` | Conectividad con PostgreSQL |
| `http://localhost:5228/scalar/v1` | Documentación (solo en Development) |
| `http://localhost:5228/openapi/v1.json` | Spec OpenAPI para generar el cliente del frontend |

Las migraciones no se aplican al arrancar. La migración `Inicial` (`Satelite.Infraestructura/Persistencia/Migraciones`) ejecuta copias de `db/init/01-esquema.sql` y `02-ajustes-etapa1.sql`. Para recrear la base local con ella y comprobar que el esquema equivale a `db/init`: `& ..\..\db\recrear-con-migracion.ps1 *>&1` (ver `db/README.md`). Un cambio de esquema posterior va en una migración nueva (`dotnet ef migrations add <Nombre> --project Satelite.Infraestructura --startup-project Satelite.Api --output-dir Persistencia/Migraciones`) y en `db/init/02-ajustes-etapa1.sql` con su marca `[E1-n]`.

## Pruebas

```powershell
dotnet test --solution AEXSA.ZARPE.BACKEND.NET.sln
```

- `Satelite.Tests/Motor`: casos T1 a T12 del motor de tarifas y reconstrucción del monto desde la memoria (sin infraestructura).
- `Satelite.Tests/Infraestructura`: PostgreSQL 16 en contenedor (Testcontainers, imagen `postgres:16-alpine`) con la migración aplicada. **Requiere Docker corriendo.** `PostgresFixture` se comparte con `[Collection(PostgresCollection.Nombre)]`.

## Autenticación y autorización

Configuración en la sección `Auth` de appsettings. Ningún valor es secreto:

| Clave | Valor |
|-------|-------|
| `Auth:Authority` | `https://cognito-idp.<region>.amazonaws.com/<userPoolId>` `[POR CONFIRMAR PA-009]` |
| `Auth:ClientId` | App client del Satélite en el pool de la empresa `[POR CONFIRMAR PA-009]` |
| `Auth:RoleClaim` | Claim con los grupos. Por defecto `cognito:groups` |
| `Auth:NameClaim` | Claim con el nombre de usuario. Por defecto `username` |
| `Auth:GruposPorRol` | Rol del Satélite → grupo del pool. `[SUPUESTO]` `zarpe-comercial`, `zarpe-facturacion`, `zarpe-recaudacion`, `zarpe-administrador` |

Qué valida la API:

1. La firma con las JWKS del pool, el emisor y la expiración.
2. Que el token sea un access token (`token_use = access`) del app client del Satélite (`client_id`). Los tokens de otras aplicaciones del pool se rechazan con 401.
3. Que el usuario tenga al menos un rol del Satélite. Si no lo tiene, recibe 403. Es la política por defecto y la de respaldo, así que todo endpoint queda protegido salvo los marcados con `AllowAnonymous`.

Si `Auth` está vacío, la API arranca en Development y deja una advertencia en el log; todos los endpoints protegidos responden 401. Fuera de Development no arranca.

### Permisos (estándar AGUNSA adaptado)

Los roles vienen de Cognito y no de tablas propias. La asignación de accesos a cada rol está en `Handlers/MatrizAccesos.cs`, con una matriz inicial `[SUPUESTO]` que se valida con negocio (PA-009).

Para agregar un módulo con permisos:

1. Agregar las constantes en `Handlers/AccessNames.cs` y en `access.constants.ts` del frontend.
2. Asignarlas a los roles en `Handlers/MatrizAccesos.cs`.
3. Proteger los endpoints:

   ```csharp
   [HttpPost("Confirmar/{id}")]
   [AccessAuthorize(AccessNames.Convenios_Confirmar)]
   public IActionResult Confirmar(Guid id) { ... }
   ```

4. Para auditar, inyectar `ICurrentUser` en el caso de uso y guardar `UsuarioAuditoria` (correo o `sub`).

### Convención de servicios

Igual que el estándar: los servicios de aplicación devuelven `ServiceResult<T>` y el controller responde con `StatusCode(result.StatusCode, result.Success ? result.Data : new { message = result.Message })`. Cada servicio nuevo se registra en `Satelite.Aplicacion/DependencyInjection.cs` (casos de uso) o en `Satelite.Infraestructura/DependencyInjection.cs` (adaptadores).

## Versión desplegada (tag de imagen Docker)

El contexto de build es la carpeta de la solución:

```bash
TAG=qa-$(date +%y%m%d)-$(date +%H%M)
docker build --build-arg IMAGE_TAG=$TAG -f Satelite.Api/Dockerfile -t <registry>/zarpe-backend:$TAG .
```

En AWS la cadena de conexión llega por la variable de entorno `ConnectionStrings__DefaultConnection`.

## Stack

- .NET 10 / C#, ASP.NET Core 10.0.15
- EF Core 10.0.15 + Npgsql 10.0.3 (PostgreSQL 16)
- JWT Bearer contra Cognito
- OpenAPI nativo + Scalar 2.21
- xUnit v3
