using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.UseCases.GetTenant;
using Shared.Domain.Tenants.Ports;
using Shared.MasterAccess.Persistence.EntityFramework;
using Shared.MasterAccess.Persistence.EntityFramework.Tenants;
using Shared.MasterAccess.Services;

namespace Shared.MasterAccess.Extensions;

public static class MasterAccessExtensions
{
    public static IServiceCollection AddMasterAccess(this IServiceCollection services, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("MasterAccessDb"));
        else
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(maxRetryCount: 3)));

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddSingleton<ITenantConnectionStringResolver, TenantConnectionStringResolver>();
        services.AddScoped<IGetTenantByCodeUseCase, GetTenantByCodeUseCase>();

        return services;
    }
}
