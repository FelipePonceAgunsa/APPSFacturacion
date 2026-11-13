using System.Text.Json.Serialization;
using Satelite.Api.Extensions;
using Satelite.Api.Services;
using Satelite.Aplicacion;
using Satelite.Aplicacion.Comun;
using Satelite.Infraestructura;
using Scalar.AspNetCore;

const string NombreServicio = "ZARPE - FACTURACIÓN API";

var builder = WebApplication.CreateBuilder(args);

// Seguridad: Cognito federado con Entra ID (ADR-016)
builder.Services.AddCognitoAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddAutorizacionSatelite();

// Capas de la solución (ADR-001)
builder.Services.AddAplicacion();
builder.Services.AddInfraestructura(builder.Configuration);

// Configurar CORS (orígenes por ambiente en Cors:AllowedOrigins)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSpecificOrigins", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = NombreServicio,
            Version = "v1",
        };
        return Task.CompletedTask;
    });
    options.AddDocumentTransformer<BearerSecuritySchemeTransformer>();
});

var app = builder.Build();

if (!app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Satelite.Api.Handlers.AuthOptions>>().Value.EstaConfigurado)
{
    app.Logger.LogWarning(
        "Auth:Authority / Auth:ClientId sin configurar (PA-009): todos los endpoints protegidos responderán 401.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.WithTitle(NombreServicio)
               .AddPreferredSecuritySchemes("Bearer")
               .AddHttpAuthentication("Bearer", bearer =>
               {
                   bearer.Token = "";
               });
    }).AllowAnonymous();
}

app.UseHttpsRedirection();

// Usar CORS
app.UseCors("AllowSpecificOrigins");

app.UseAuthentication();

app.UseAuthorization();

// Versión desplegada: sale del tag de la imagen Docker (--build-arg IMAGE_TAG)
app.MapGet("/", (IConfiguration config) => Results.Ok(new
{
    status = "Healthy",
    service = NombreServicio,
    version = config["IMAGE_TAG"] ?? "local"
})).AllowAnonymous().ExcludeFromDescription();

// Liveness (health check del ALB): no depende de la base
app.MapGet("/health", () => Results.Ok()).AllowAnonymous().ExcludeFromDescription();

// Conectividad con PostgreSQL
app.MapHealthChecks("/health/db").AllowAnonymous();

app.MapControllers();

app.Run();

// Visible para WebApplicationFactory en Satelite.Tests
public partial class Program;
