using Api.DependencyInjection;
using Api.HostedServices;
using Api.Session;
using Infrastructure.MasterAccess.Extensions;
using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Application.Ports;
using Shared.Results;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real application against the Testcontainers infrastructure. The app has no
/// single-tenant mode and no in-memory persistence, so multitenancy is switched on and the only
/// thing faked is the external dependency the suite cannot host: the tenant-resolver, replaced by a
/// stub that hands back the container's database for every tenant code. Everything below it — the
/// tenant middleware, the per-tenant <c>DbContext</c>, SQL Server — is the production wiring.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    /// <summary>
    /// Only has to parse as an absolute URL: both callers of it (the typed resolver client and the
    /// startup probe) are removed below, so no request is ever sent here.
    /// </summary>
    private const string UnusedResolverBaseUrl = "http://tenant-resolver.invalid";

    private readonly string _connectionString;

    public ApiFactory(string connectionString, string redisConnectionString)
    {
        _connectionString = connectionString;

        // Environment variables, not ConfigureAppConfiguration: Program.cs reads the multitenancy
        // prerequisites eagerly, before a single service is registered, and the callback would run
        // too late. The providers CreateBuilder initializes read the environment early enough.
        Environment.SetEnvironmentVariable("Sentry__Enabled", "false");
        Environment.SetEnvironmentVariable("Sentry__Dsn", string.Empty);
        Environment.SetEnvironmentVariable("SENTRY_DSN", string.Empty);
        Environment.SetEnvironmentVariable("TenantResolverService__Enabled", "true");
        Environment.SetEnvironmentVariable(
            TenancyConfigurationExtensions.TenantResolverUrlVariable, UnusedResolverBaseUrl);
        Environment.SetEnvironmentVariable(
            TenancyConfigurationExtensions.EncryptionKeyVariable, "integration-tests");
        Environment.SetEnvironmentVariable("Cache__L2Enabled", "true");
        Environment.SetEnvironmentVariable("Cache__ConnectionString", redisConnectionString);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // ConfigureTestServices runs after the app registered its own services, which is what makes
        // replacing an already-made registration possible.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITenantResolverServiceClient>();
            services.AddSingleton<ITenantResolverServiceClient>(new StubTenantResolver(_connectionString));

            // Fixed rather than per-request: IntegrationTestBase resolves the DbContext from a scope
            // of its own, outside any request, where the tenant middleware never runs.
            services.RemoveAll<ITenantConnectionInitializer>();
            services.RemoveAll<IDbConnectionProvider>();
            services.AddScoped<ITenantConnectionInitializer>(_ => new FixedTenantConnection(_connectionString));
            services.AddScoped<IDbConnectionProvider>(sp =>
                (IDbConnectionProvider)sp.GetRequiredService<ITenantConnectionInitializer>());

            RemoveTenantResolverStartupProbe(services);
            RemoveTenantResolverHealthCheck(services);
        });

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.SetMinimumLevel(LogLevel.Warning);
        });
    }

    /// <summary>
    /// Drops the boot gate: it aborts startup when the resolver's <c>/health</c> is unreachable, and
    /// here it never is.
    /// </summary>
    private static void RemoveTenantResolverStartupProbe(IServiceCollection services)
    {
        var probe = services.FirstOrDefault(descriptor =>
            descriptor.ImplementationType == typeof(TenantResolverStartupProbe));

        if (probe is not null)
            services.Remove(probe);
    }

    /// <summary>
    /// Drops the readiness check aimed at the same unreachable resolver, so <c>/health/ready</c>
    /// reports on what the suite actually runs. PostConfigure runs after every registration.
    /// </summary>
    private static void RemoveTenantResolverHealthCheck(IServiceCollection services)
    {
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            var registration = options.Registrations.FirstOrDefault(r =>
                r.Name == InfrastructureServiceExtensions.TenantInfoHealthCheckName);

            if (registration is not null)
                options.Registrations.Remove(registration);
        });
    }

    /// <summary>
    /// Stands in for the external tenant-resolver: every tenant code resolves to the Testcontainers
    /// database, so requests exercise the real middleware-to-DbContext path without one running.
    /// </summary>
    private sealed class StubTenantResolver(string connectionString) : ITenantResolverServiceClient
    {
        public Task<Result<TenantInfo>> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
            Task.FromResult<Result<TenantInfo>>(new TenantInfo(code, "integration-tests", connectionString));
    }

    /// <summary>
    /// The test counterpart of <c>TenantContext</c>: same two segregated interfaces over one scoped
    /// instance, but seeded with the container connection string so a <c>DbContext</c> resolved
    /// outside a request works too.
    /// </summary>
    private sealed class FixedTenantConnection(string initialConnectionString)
        : IDbConnectionProvider, ITenantConnectionInitializer
    {
        public string ConnectionString { get; private set; } = initialConnectionString;

        public void Initialize(string connectionString) => ConnectionString = connectionString;
    }
}
