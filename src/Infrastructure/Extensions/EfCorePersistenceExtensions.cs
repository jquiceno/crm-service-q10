using Infrastructure.Adapters.Persistence;
using Infrastructure.Persistence.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Ports;

namespace Infrastructure.Extensions;

public static class EfCorePersistenceExtensions
{
    public static IServiceCollection AddEfCoreSqlServer(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString),
                "Connection string cannot be null or empty.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

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
    }
}
