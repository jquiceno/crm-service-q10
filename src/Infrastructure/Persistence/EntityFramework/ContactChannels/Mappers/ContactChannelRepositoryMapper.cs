using ContactChannel.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels.Mappers;

public static class ContactChannelRepositoryMapper
{
    public static ContactChannelAggregate ToDomain(ContactChannelEntity document) =>
        ContactChannelAggregate.Reconstruct(
            document.Id,
            document.Name,
            document.IsActive);

    public static ContactChannelEntity ToDocument(ContactChannelAggregate aggregate) =>
        new()
        {
            Id = aggregate.Id,
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,
        };
}
