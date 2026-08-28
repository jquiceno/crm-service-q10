using System.ComponentModel;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public sealed record UpdateContactChannelOutputDto(
    [property: Description("Identifier of the contact channel.")]
    int Id,
    [property: Description("Name of the contact channel.")]
    string Name,
    [property: Description("Whether the contact channel is active and available for selection.")]
    bool IsActive);
