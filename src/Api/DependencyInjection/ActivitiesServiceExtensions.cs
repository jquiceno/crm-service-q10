using Activities.Application.UseCases.CreateActivity;
using Activities.Application.UseCases.GetActivities;

namespace Api.DependencyInjection;

/// <summary>
/// Use cases of the Activities context.
/// </summary>
/// <remarks>
/// Scoped, like the <c>DbContext</c>, the repository and the unit of work they share within a
/// request. The repository and the readers they depend on are registered next to the rest of the
/// persistence, in <c>EfCorePersistenceExtensions</c>.
/// </remarks>
public static class ActivitiesServiceExtensions
{
    public static IServiceCollection AddActivitiesServices(this IServiceCollection services)
    {
        services.AddScoped<IGetActivitiesUseCase, GetActivitiesUseCase>();
        services.AddScoped<ICreateActivityUseCase, CreateActivityUseCase>();

        return services;
    }
}
