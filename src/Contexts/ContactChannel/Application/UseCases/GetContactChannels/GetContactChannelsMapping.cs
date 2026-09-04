using ContactChannel.Domain.Aggregates;

namespace ContactChannel.Application.UseCases.GetContactChannels;

public static class GetContactChannelsMapping
{
    public static GetContactChannelsOutputDto ToOutputDto(this ContactChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
