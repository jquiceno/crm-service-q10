using ContactChannel.Domain.Aggregates;
using Shared.Results;

namespace ContactChannel.Application.UseCases.CreateContactChannel;

public static class CreateContactChannelMapping
{
    public static Result<ContactChannelAggregate> ToAggregate(this CreateContactChannelInputDto input) =>
        ContactChannelAggregate.Create(new CreateContactChannelArgs(input.Name, input.IsActive!.Value));

    public static CreateContactChannelOutputDto ToOutputDto(this ContactChannelAggregate aggregate) =>
        new(aggregate.Id, aggregate.Name, aggregate.IsActive);
}
