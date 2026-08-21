using System.ComponentModel;

namespace AdsChannel.Application.UseCases.UpdateAdsChannel;

public sealed record UpdateAdsChannelInputDto(
    [property: Description("New name of the ads channel. Required, up to 100 characters.")]
    string? Name,
    [property: Description("Whether the ads channel is active.")]
    bool IsActive);
