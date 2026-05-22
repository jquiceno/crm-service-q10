using Api.DependencyInjection;
using Api.Filters;
using Api.Middleware;
using Api.Routing;
using Infrastructure.Extensions;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

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
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new()
            {
                Title = "Weather Forecast API",
                Version = "v1",
                Description = "API for the weather forecast application",
                Contact = new OpenApiContact
                {
                    Name = "Weather Forecast API",
                    Email = "weatherforecast@example.com",
                    Url = new Uri("https://www.weatherforecast.com")
                }
            };
            return Task.CompletedTask;
        });
    });
}

var app = builder.Build();

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
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
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
