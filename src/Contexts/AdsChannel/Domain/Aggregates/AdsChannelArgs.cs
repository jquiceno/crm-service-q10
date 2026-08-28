namespace AdsChannel.Domain.Aggregates;

public sealed record CreateAdsChannelArgs(string? Name, bool IsActive = true);

public sealed record UpdateAdsChannelArgs(string? Name, bool IsActive);
