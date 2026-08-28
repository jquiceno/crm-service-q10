using AdsChannel.Domain.Aggregates;
using Shared.Results;

namespace AdsChannel.Application.UseCases.CreateAdsChannel;

public static class CreateAdsChannelMapping
{
    public static Result<AdsChannelAggregate> ToAggregate(this CreateAdsChannelInputDto input) =>
        AdsChannelAggregate.Create(new CreateAdsChannelArgs(input.Name, input.IsActive));

    public static CreateAdsChannelOutputDto ToOutputDto(this AdsChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
