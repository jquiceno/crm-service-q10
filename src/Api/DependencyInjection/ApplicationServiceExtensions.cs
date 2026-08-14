namespace Api.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSharedServices();
        services.AddServiceInfoServices();

        return services;
    }
}
