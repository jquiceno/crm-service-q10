using ContactChannel.Domain.Repositories;
using Infrastructure.Persistence.EntityFramework.ContactChannels;

namespace Api.DependencyInjection;

public static class ContactChannelServiceExtensions
{
    public static IServiceCollection AddContactChannelServices(this IServiceCollection services)
    {
        services.AddScoped<IContactChannelRepository, ContactChannelRepository>();

        return services;
    }
}
