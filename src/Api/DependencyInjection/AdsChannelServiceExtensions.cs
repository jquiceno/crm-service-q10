using AdsChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.AdsChannels;

namespace Api.DependencyInjection;

public static class AdsChannelServiceExtensions
{
    public static IServiceCollection AddAdsChannelServices(this IServiceCollection services)
    {
        services.AddScoped<IAdsChannelRepository, AdsChannelRepository>();

        // Use cases are added here as each vertical slice lands (F3.1-F3.5).

        return services;
    }
}
