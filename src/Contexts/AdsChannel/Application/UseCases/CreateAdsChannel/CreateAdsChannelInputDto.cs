using System.ComponentModel;

namespace AdsChannel.Application.UseCases.CreateAdsChannel;

public sealed record CreateAdsChannelInputDto(
    [property: Description("Name of the ads channel. Required, up to 100 characters.")]
    string? Name,
    [property: Description("Whether the ads channel is active. Defaults to true.")]
    bool IsActive = true);
