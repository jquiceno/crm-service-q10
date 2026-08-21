using System.ComponentModel;

namespace ContactChannel.Application.UseCases.CreateContactChannel;

public sealed record CreateContactChannelOutputDto(
    [property: Description("Identifier the database generated for the contact channel.")]
    int Id,
    [property: Description("Name of the contact channel.")]
    string Name,
    [property: Description("Whether the contact channel is available for selection.")]
    bool IsActive);
