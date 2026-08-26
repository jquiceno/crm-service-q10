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

builder.Configuration.AddTenantResolverEnvironmentAliases();

var multitenancyEnabled = builder.Configuration.IsMultitenancyEnabled();

// Single source of truth for the service URL prefix (matches the ingress path). Applied to
// controllers via GlobalRoutePrefixConvention and to the minimal-API endpoints below.
var routePrefix = (builder.Configuration["RoutePrefix"] ?? string.Empty).Trim('/');

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
        if (!string.IsNullOrWhiteSpace(routePrefix))
            options.Conventions.Add(new GlobalRoutePrefixConvention(routePrefix));
        options.Filters.Add<ValidateRequestFilter>();
    });

var app = builder.Build();

// Prepends the service prefix to a root-relative path (e.g. "/health/ready"). Minimal-API endpoints
// are not controllers, so they are prefixed here rather than by GlobalRoutePrefixConvention.
string Prefixed(string path) => string.IsNullOrEmpty(routePrefix) ? path : $"/{routePrefix}{path}";

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

app.UseOpenApiDocumentation(routePrefix);

app.MapHealthChecks(Prefixed("/health/live"), new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks(Prefixed("/health/ready"), new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

// Required for WebApplicationFactory<Program> in integration tests.
public partial class Program { }
