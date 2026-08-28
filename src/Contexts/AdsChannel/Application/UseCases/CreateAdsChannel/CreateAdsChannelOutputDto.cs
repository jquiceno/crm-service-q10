using System.ComponentModel;

namespace AdsChannel.Application.UseCases.CreateAdsChannel;

public sealed record CreateAdsChannelOutputDto(
    [property: Description("Identifier assigned to the newly created ads channel.")]
    int Id,
    [property: Description("Name of the ads channel.")]
    string Name,
    [property: Description("Whether the ads channel is active.")]
    bool IsActive);
