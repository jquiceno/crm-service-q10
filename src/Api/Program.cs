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

var routePrefix = builder.Configuration.GetRoutePrefix();
if (string.IsNullOrEmpty(routePrefix))
    throw new InvalidOperationException(
        $"Configuration key '{RoutePrefixConfig.ConfigKey}' is required and must be non-empty " +
        "(e.g. \"/service-template\"). It sets the URL prefix every endpoint is served under.");

builder.Services
    .AddApiSettings(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddSessionServices(builder.Configuration)
    .ConfigureCache(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddApiErrorHandling()
    .AddOpenApiDocumentation(builder.Environment)
    .AddControllers(options =>
    {
        options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
        options.Conventions.Add(new GlobalRoutePrefixConvention(routePrefix));
        options.Filters.Add<ValidateRequestFilter>();
    });

var app = builder.Build();

string Prefixed(string path) => RoutePrefixConfig.BasePath(routePrefix) + path;

app.UseExceptionHandler();

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering();
    await next().ConfigureAwait(false);
});

app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors(CorsExtensions.CorsPolicyName);

app.UseTenantResolution();

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
