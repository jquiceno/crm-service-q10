using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Queries;

namespace ContactChannel.Application.UseCases.GetContactChannels;

public static class GetContactChannelsMapping
{
    public static ContactChannelFilter ToFilter(this GetContactChannelsInputDto input) =>
        new(input.IsActive, input.SearchName);

    public static GetContactChannelsOutputDto ToOutputDto(this ContactChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
