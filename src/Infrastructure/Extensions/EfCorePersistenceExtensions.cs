using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;

namespace Infrastructure.Extensions;

public static class EfCorePersistenceExtensions
{
    /// <summary>
    /// Registers the only persistence mode the service supports: SQL Server resolved per request from
    /// <see cref="IDbConnectionProvider"/>. There is deliberately no in-memory fallback — a boot without
    /// a real database would look healthy while silently dropping every write.
    /// </summary>
    public static IServiceCollection AddEfCoreSqlServerPerTenant(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<IDbConnectionProvider>().ConnectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<IUnitOfWorkPort, UnitOfWorkAdapter>();

        return services;
    }
}
