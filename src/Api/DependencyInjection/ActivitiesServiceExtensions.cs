using Activities.Application.Ports;
using Activities.Application.UseCases.CreateActivity;
using Activities.Application.UseCases.GetActivities;
using Activities.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.Activities;

namespace Api.DependencyInjection;

/// <summary>
/// Everything the Activities context needs resolved: its persistence, its readers and its use
/// cases. This is the one place where an interface of the context meets its implementation.
/// </summary>
/// <remarks>
/// All scoped, like the <c>DbContext</c> they share within a request. The persistence pieces are
/// registered before the use cases that consume them.
/// </remarks>
public static class ActivitiesServiceExtensions
{
    public static IServiceCollection AddActivitiesServices(this IServiceCollection services)
    {
        services.AddScoped<IActivityRepository, ActivityRepository>();
        services.AddScoped<IDealReader, DealReader>();
        services.AddScoped<IAdvisorReader, AdvisorReader>();

        services.AddScoped<IGetActivitiesUseCase, GetActivitiesUseCase>();
        services.AddScoped<ICreateActivityUseCase, CreateActivityUseCase>();

        return services;
    }
}
