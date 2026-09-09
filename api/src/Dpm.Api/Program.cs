using Dpm.Api.Extensions;
using Dpm.Api.Middleware;
using Dpm.Catalog.Api;
using Dpm.Downloads.Api;
using Dpm.Files.Api;
using Dpm.Identity.Api;
using Dpm.Ledger.Api;
using Dpm.Orders.Api;
using Dpm.Outbox;
using Dpm.Payments.Api;
using Dpm.Marketplace.Api;
using Dpm.SharedApi;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VicxelStore API",
        Version = "v1",
        Description = "Multi-vendor digital product marketplace.",
    });

    options.AddSecurityDefinition("bearerAuth", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "bearerAuth" },
        }] = Array.Empty<string>(),
    });
});

builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddMarketplaceModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddFilesModule(builder.Configuration);
builder.Services.AddOrdersModule(builder.Configuration);
builder.Services.AddPaymentsModule(builder.Configuration);
builder.Services.AddLedgerModule(builder.Configuration);
builder.Services.AddDownloadsModule(builder.Configuration);
builder.Services.AddOutboxDispatcher(builder.Configuration);
builder.Services.AddApiAuthentication();
builder.Services.AddApiRateLimiting();

var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173"];
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy => policy
        .WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

var connectionString = builder.Configuration.GetConnectionString("Database");
var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(connectionString))
{
    healthChecks.AddSqlServer(connectionString, name: "database", tags: ["ready"]);
}

var app = builder.Build();

app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous();
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();

await app.RunAsync();

/// <summary>Exposed so integration tests can drive the app with WebApplicationFactory.</summary>
public partial class Program;
