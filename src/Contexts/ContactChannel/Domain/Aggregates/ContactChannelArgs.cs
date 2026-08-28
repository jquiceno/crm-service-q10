namespace ContactChannel.Domain.Aggregates;

public sealed record CreateContactChannelArgs(string? Name, bool? IsActive);

public sealed record UpdateContactChannelArgs(string? Name, bool? IsActive);
