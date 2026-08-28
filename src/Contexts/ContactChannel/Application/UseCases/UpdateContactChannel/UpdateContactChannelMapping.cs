using ContactChannel.Domain.Aggregates;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public static class UpdateContactChannelMapping
{
    public static UpdateContactChannelArgs ToUpdateArgs(this UpdateContactChannelInputDto input) =>
        new(input.Name, input.IsActive);

    public static UpdateContactChannelOutputDto ToOutputDto(this ContactChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
