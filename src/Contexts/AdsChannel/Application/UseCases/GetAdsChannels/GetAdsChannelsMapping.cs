using AdsChannel.Domain.Aggregates;

namespace AdsChannel.Application.UseCases.GetAdsChannels;

public static class GetAdsChannelsMapping
{
    public static GetAdsChannelsOutputDto ToOutputDto(this AdsChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
