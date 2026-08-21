using BusinessStatus.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;

namespace Api.DependencyInjection;

public static class BusinessStatusServiceExtensions
{
    public static IServiceCollection AddBusinessStatusServices(this IServiceCollection services)
    {
        services.AddScoped<IBusinessStatusRepository, BusinessStatusRepository>();

        return services;
    }
}
