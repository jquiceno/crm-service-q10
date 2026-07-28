using Infrastructure.Persistence.EntityFramework;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IntegrationTests.Infrastructure;

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public ApiFactory(string connectionString)
    {
        _connectionString = connectionString;

        Environment.SetEnvironmentVariable("Sentry__Dsn", string.Empty);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Multitenancy stays off: turning it on would require a reachable tenant-resolver
        // (AddInfrastructureServices aborts startup without one, and TenantResolverStartupProbe
        // blocks the boot) plus an L2 Redis. Instead the app boots on its in-memory default and
        // the DbContext is re-pointed here at the Testcontainers SQL Server, so the tests exercise
        // the real provider — translated queries, constraints, transactions — against the same
        // database SqlServerContainerFixture creates the schema in and DatabaseResetter clears
        // between tests.
        builder.ConfigureTestServices(services =>
        {
            RemoveApplicationDbContextRegistrations(services);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    /// <summary>
    /// Drops the app's <see cref="ApplicationDbContext"/> registrations: the context, its options,
    /// and EF's internal options-configuration callbacks — matched broadly as anything generic
    /// closed over the context type. Removing less leaves the app's <c>UseInMemoryDatabase</c>
    /// callback next to the <c>UseSqlServer</c> one, which makes EF throw on resolve instead of
    /// silently downgrading the suite to in-memory.
    /// </summary>
    private static void RemoveApplicationDbContextRegistrations(IServiceCollection services)
    {
        var stale = services
            .Where(descriptor =>
                descriptor.ServiceType == typeof(ApplicationDbContext)
                || (descriptor.ServiceType.IsGenericType
                    && Array.IndexOf(
                        descriptor.ServiceType.GetGenericArguments(),
                        typeof(ApplicationDbContext)) >= 0))
            .ToList();

        foreach (var descriptor in stale)
        {
            services.Remove(descriptor);
        }
    }
}
