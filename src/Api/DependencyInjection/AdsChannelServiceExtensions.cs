using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.AdsChannels;

namespace Api.DependencyInjection;

public static class AdsChannelServiceExtensions
{
    public static IServiceCollection AddAdsChannelServices(this IServiceCollection services)
    {
        services.AddScoped<IAdsChannelRepository, AdsChannelRepository>();

        services.AddScoped<ICreateAdsChannelUseCase, CreateAdsChannelUseCase>();

        return services;
    }
}
