using ContactChannel.Domain.Aggregates;

namespace ContactChannel.Application.UseCases.GetContactChannelById;

public static class GetContactChannelByIdMapping
{
    public static GetContactChannelByIdOutputDto ToOutputDto(this ContactChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
