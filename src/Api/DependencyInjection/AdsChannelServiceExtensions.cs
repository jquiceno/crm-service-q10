using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Application.UseCases.DeleteAdsChannel;
using AdsChannel.Application.UseCases.UpdateAdsChannel;
using AdsChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.AdsChannels;

namespace Api.DependencyInjection;

public static class AdsChannelServiceExtensions
{
    public static IServiceCollection AddAdsChannelServices(this IServiceCollection services)
    {
        services.AddScoped<IAdsChannelRepository, AdsChannelRepository>();

        services.AddScoped<ICreateAdsChannelUseCase, CreateAdsChannelUseCase>();
        services.AddScoped<IUpdateAdsChannelUseCase, UpdateAdsChannelUseCase>();
        services.AddScoped<IDeleteAdsChannelUseCase, DeleteAdsChannelUseCase>();

        return services;
    }
}
