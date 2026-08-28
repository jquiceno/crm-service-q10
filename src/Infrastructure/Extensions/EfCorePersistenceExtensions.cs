using Activities.Application.Ports;
using Activities.Domain.Repositories;
using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Activities;
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
        services.AddScoped<IUnitOfWorkPort, UnitOfWorkAdapter>();

        // Activities context — the aggregate's repository and the readers of the institution's
        // foreign tables. Registered next to the context's persistence pieces, before the use
        // cases that consume them.
        services.AddScoped<IActivityRepository, ActivityRepositoryAdapter>();
        services.AddScoped<IDealReader, DealReader>();
        services.AddScoped<IAdvisorReader, AdvisorReader>();
    }
}
