using Api.DependencyInjection;
using Api.Filters;
using Api.Middleware;
using Shared.Presentation.Routing;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("template.json", optional: false, reloadOnChange: false);
builder.Configuration.AddAzureKeyVault(builder.Environment);
builder.AddSentry();
builder.Host.AddSerilog(builder.Configuration);

builder.Services
    .AddApiSettings(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .ConfigureCache(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddApiErrorHandling()
    .AddControllers(options =>
    {
        options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
        options.Filters.Add<ValidateRequestFilter>();
    });

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "Service API",
                Version = "v1",
                Description = "API description",
                Contact = new OpenApiContact
                {
                    Name = "Service API",
                    Email = "info@example.com",
                    Url = new Uri("https://example.com")
                }
            };
            return Task.CompletedTask;
        });

        options.AddSchemaTransformer((schema, context, _) =>
        {
            // Enums may surface as Nullable<TEnum>, whose own IsEnum is false; unwrap first.
            var enumType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (enumType.IsEnum)
            {
                var lines = Enum.GetValues(enumType)
                    .Cast<object>()
                    .Select(v => $"- `{Convert.ToInt32(v)}` = {v}");
                schema.Description = $"{schema.Description}\n\n{string.Join("\n", lines)}".Trim();
            }
            return Task.CompletedTask;
        });
    });
}

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

app.UseCacheMiddleware();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference("/openapi", options =>
    {
        options.OpenApiRoutePattern = "/openapi/{documentName}.json";
    });
    app.MapGet("/openapi", () => Results.Redirect("/openapi/v1")).ExcludeFromDescription();
}

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
