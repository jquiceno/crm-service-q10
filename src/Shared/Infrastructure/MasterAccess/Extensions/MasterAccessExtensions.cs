using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;
using Infrastructure.MasterAccess.Resolvers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.UseCases.GetTenant;
using Shared.Domain.Tenants.Ports;

namespace Infrastructure.MasterAccess.Extensions;

public static class MasterAccessExtensions
{
    public static IServiceCollection AddMasterAccess(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("Connection string for master access is required.", nameof(connectionString));
        }

        services.AddDbContext<MasterAccessDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddSingleton<ITenantConnectionStringResolver, TenantConnectionStringResolver>();
        services.AddScoped<IGetTenantByCodeUseCase, GetTenantByCodeUseCase>();

        return services;
    }
}
