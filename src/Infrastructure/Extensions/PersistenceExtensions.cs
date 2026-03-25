using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Infrastructure.Extensions;

public static class PersistenceExtensions
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString),
                "Connection string cannot be null or empty.");
        }

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
                sqlOptions.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddRepositories();

        return services;
    }

    public static IServiceCollection AddInMemoryPersistence(this IServiceCollection services)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase("InMemoryDb"));

        services.AddRepositories();

        return services;
    }

    private static void AddRepositories(this IServiceCollection services)
    {
        services.AddScoped(
            typeof(WeatherForecast.Domain.Interfaces.IRepository<>),
            typeof(Persistence.WeatherForecast.Repository<>));
    }
}