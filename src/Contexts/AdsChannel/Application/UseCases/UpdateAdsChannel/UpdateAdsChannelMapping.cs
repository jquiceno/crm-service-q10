using AdsChannel.Domain.Aggregates;

namespace AdsChannel.Application.UseCases.UpdateAdsChannel;

public static class UpdateAdsChannelMapping
{
    public static UpdateAdsChannelArgs ToUpdateArgs(this UpdateAdsChannelInputDto input) =>
        new(input.Name, input.IsActive);

    public static UpdateAdsChannelOutputDto ToOutputDto(this AdsChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
