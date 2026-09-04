using System.ComponentModel;

namespace ContactChannel.Application.UseCases.GetContactChannels;

public sealed record GetContactChannelsInputDto(
    [property: Description(
        "Filters by active state. When omitted, both active and inactive contact channels are returned.")]
    bool? IsActive,
    [property: Description(
        "Free-text filter. Matches a fragment of the contact channel name. "
        + "Maximum 200 characters; a longer value answers 400.")]
    string? Search);
