using System.ComponentModel;

namespace AdsChannel.Application.UseCases.GetAdsChannels;

public sealed record GetAdsChannelsInputDto(
    [property: Description("Filters ads channels whose name contains this text. Case-insensitive.")]
    string? NameContains,
    [property: Description("Filters ads channels by active status. Omit to return both active and inactive.")]
    bool? IsActive);
