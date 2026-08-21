namespace ContactChannel.Domain.Queries;

public sealed record ContactChannelFilter(bool? IsActive, string? SearchName);
