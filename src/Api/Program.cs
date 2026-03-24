using Api.DependencyInjection;
using Api.Middleware;
using Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddAzureKeyVault();
builder.Host.AddLoggingConfiguration(builder.Configuration);

builder.Services
    .AddApiSettings(builder.Configuration)
    .AddApplicationServices()
    .AddInfrastructureServices(builder.Configuration)
    .AddCorsPolicy(builder.Configuration)
    .AddControllers();

builder.Services.AddHealthChecks();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
}

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors(CorsExtensions.CorsPolicyName);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapHealthChecks("/health");
app.MapControllers();

app.Run();
