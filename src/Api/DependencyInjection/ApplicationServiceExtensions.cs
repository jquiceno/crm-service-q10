namespace Api.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSharedServices();
        services.AddContactChannelServices();
        services.AddServiceInfoServices();

        return services;
    }
}
