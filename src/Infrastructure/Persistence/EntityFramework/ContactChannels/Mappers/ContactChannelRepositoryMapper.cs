using ContactChannel.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;

namespace Infrastructure.Persistence.EntityFramework.ContactChannels.Mappers;

public static class ContactChannelRepositoryMapper
{
    public static ContactChannelAggregate ToDomain(ContactChannelEntity document) =>
        ContactChannelAggregate.Reconstruct(
            document.Id,
            document.Name ?? string.Empty,
            document.IsActive ?? false);

    public static ContactChannelEntity ToDocument(ContactChannelAggregate aggregate) =>
        new()
        {
            Id = aggregate.Id,
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,
        };

    public static ContactChannelEntity ToNewDocument(ContactChannelAggregate aggregate) =>
        new()
        {
            Name = aggregate.Name,
            IsActive = aggregate.IsActive,
        };

    public static void CopyTo(ContactChannelAggregate aggregate, ContactChannelEntity document)
    {
        document.Name = aggregate.Name;
        document.IsActive = aggregate.IsActive;
    }
}
