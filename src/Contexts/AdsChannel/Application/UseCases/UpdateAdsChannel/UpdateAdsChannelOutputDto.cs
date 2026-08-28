using System.ComponentModel;

namespace AdsChannel.Application.UseCases.UpdateAdsChannel;

public sealed record UpdateAdsChannelOutputDto(
    [property: Description("Identifier of the ads channel.")]
    int Id,
    [property: Description("Name of the ads channel.")]
    string Name,
    [property: Description("Whether the ads channel is active.")]
    bool IsActive);
