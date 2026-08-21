using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatusById;
using BusinessStatus.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.BusinessStatuses;

namespace Api.DependencyInjection;

public static class BusinessStatusServiceExtensions
{
    public static IServiceCollection AddBusinessStatusServices(this IServiceCollection services)
    {
        services.AddScoped<IBusinessStatusRepository, BusinessStatusRepository>();

        services.AddScoped<ICreateBusinessStatusUseCase, CreateBusinessStatusUseCase>();
        services.AddScoped<IGetBusinessStatusByIdUseCase, GetBusinessStatusByIdUseCase>();

        return services;
    }
}
