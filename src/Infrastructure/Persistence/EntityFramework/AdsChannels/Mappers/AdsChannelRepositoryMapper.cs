using AdsChannel.Domain.Aggregates;

namespace Infrastructure.Persistence.EntityFramework.AdsChannels.Mappers;

public static class AdsChannelRepositoryMapper
{
    public static AdsChannelAggregate ToDomain(Entities.AdsChannel document) =>
        AdsChannelAggregate.Reconstruct(document.Id, document.Name, document.IsActive);

    public static Entities.AdsChannel ToDocument(AdsChannelAggregate aggregate) =>
        new()
        {
            Id = aggregate.Id,
            Name = aggregate.Name,
            IsActive = aggregate.IsActive
        };
}
