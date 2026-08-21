using System.ComponentModel;

namespace ContactChannel.Application.UseCases.CreateContactChannel;

public sealed record CreateContactChannelInputDto(
    [property: Description(
        "Name of the contact channel. Required, maximum 100 characters, trimmed before it is stored.")]
    string? Name,
    [property: Description("Whether the contact channel is available for selection.")]
    bool IsActive);
