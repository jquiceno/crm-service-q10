using System.ComponentModel;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public sealed record UpdateContactChannelInputDto(
    [property: Description(
        "Name of the contact channel. Required, maximum 100 characters, trimmed before it is stored.")]
    string? Name,
    [property: Description("Whether the contact channel is available for selection. Required.")]
    bool? IsActive);
