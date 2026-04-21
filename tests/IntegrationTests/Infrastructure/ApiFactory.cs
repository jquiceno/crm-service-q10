using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    public ApiFactory(string connectionString)
    {
        // In .NET 8 minimal hosting, Program.cs reads configuration eagerly during
        // service registration (AddInfrastructureServices), which runs BEFORE the
        // factory's ConfigureAppConfiguration callback. Env vars are picked up by
        // CreateBuilder's default providers, so they reach config in time.
        Environment.SetEnvironmentVariable("Persistence__Enabled", "true");
        Environment.SetEnvironmentVariable("Persistence__ConnectionString", connectionString);
        Environment.SetEnvironmentVariable("Sentry__Dsn", string.Empty);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }
}
