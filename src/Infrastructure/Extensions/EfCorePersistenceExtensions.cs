using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;

namespace Infrastructure.Extensions;

public static class EfCorePersistenceExtensions
{
    public static IServiceCollection AddEfCoreSqlServerPerTenant(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
            options.UseSqlServer(
                sp.GetRequiredService<IDbConnectionProvider>().ConnectionString,
                sqlOptions => sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

        RegisterPersistenceServices(services);

        return services;
    }

    public static IServiceCollection AddEfCoreInMemory(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("InMemoryDb"));

        RegisterPersistenceServices(services);

        return services;
    }

    private static void RegisterPersistenceServices(IServiceCollection services)
    {
        // Only what belongs to every context. A context's own repository and readers are
        // registered in its Add{Context}Services, the single place where an interface and its
        // implementation meet.
        services.AddScoped<IUnitOfWorkPort, UnitOfWorkAdapter>();
    }
}
