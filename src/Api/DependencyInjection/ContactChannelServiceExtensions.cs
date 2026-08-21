using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Application.UseCases.GetContactChannelById;
using ContactChannel.Application.UseCases.GetContactChannels;
using ContactChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.ContactChannels;

namespace Api.DependencyInjection;

public static class ContactChannelServiceExtensions
{
    public static IServiceCollection AddContactChannelServices(this IServiceCollection services)
    {
        services.AddScoped<IContactChannelRepository, ContactChannelRepository>();

        services.AddScoped<IGetContactChannelsUseCase, GetContactChannelsUseCase>();
        services.AddScoped<IGetContactChannelByIdUseCase, GetContactChannelByIdUseCase>();
        services.AddScoped<ICreateContactChannelUseCase, CreateContactChannelUseCase>();

        return services;
    }
}
