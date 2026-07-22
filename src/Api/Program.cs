using Api.DependencyInjection;
using Api.Filters;
using Api.Middleware;
using Shared.Presentation.Routing;
using Infrastructure.Extensions;
using Infrastructure.MasterAccess.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("template.json", optional: false, reloadOnChange: false);
builder.Configuration.AddAzureKeyVault(builder.Environment);
builder.AddSentry();
builder.Host.AddSerilog(builder.Configuration);

var multitenancyEnabled = builder.Configuration.IsMultitenancyEnabled();

builder.Services
    .AddApiSettings(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration, multitenancyEnabled)
    .AddSessionServices(builder.Configuration, multitenancyEnabled)
    .ConfigureCache(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddApiErrorHandling()
    .AddOpenApiDocumentation(builder.Environment)
    .AddControllers(options =>
    {
        options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
        options.Filters.Add<ValidateRequestFilter>();
    });

var app = builder.Build();

var pathBase = builder.Configuration["ASPNETCORE_PATHBASE"] ?? "";
if (!string.IsNullOrEmpty(pathBase)) app.UsePathBase(pathBase);

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next().ConfigureAwait(false);
});

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors(CorsExtensions.CorsPolicyName);

app.UseTenantResolution(multitenancyEnabled);

app.UseCacheMiddleware();

app.UseOpenApiDocumentation();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
