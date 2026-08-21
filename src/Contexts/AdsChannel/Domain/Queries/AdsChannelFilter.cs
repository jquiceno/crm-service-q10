namespace AdsChannel.Domain.Queries;

public sealed record AdsChannelFilter(string? NameContains, bool? IsActive);
