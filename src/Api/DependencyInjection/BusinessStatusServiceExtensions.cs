using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;

namespace Api.DependencyInjection;

public static class BusinessStatusServiceExtensions
{
    public static IServiceCollection AddBusinessStatusServices(this IServiceCollection services)
    {
        services.AddScoped<IBusinessStatusRepository, BusinessStatusRepository>();

        services.AddScoped<IGetBusinessStatusesUseCase, GetBusinessStatusesUseCase>();
        services.AddScoped<ICreateBusinessStatusUseCase, CreateBusinessStatusUseCase>();

        return services;
    }
}
