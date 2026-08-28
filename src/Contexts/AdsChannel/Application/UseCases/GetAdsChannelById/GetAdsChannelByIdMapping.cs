using AdsChannel.Domain.Aggregates;

namespace AdsChannel.Application.UseCases.GetAdsChannelById;

public static class GetAdsChannelByIdMapping
{
    public static GetAdsChannelByIdOutputDto ToOutputDto(this AdsChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
