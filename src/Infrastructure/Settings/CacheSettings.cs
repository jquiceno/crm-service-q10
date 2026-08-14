using System.ComponentModel.DataAnnotations;

namespace Infrastructure.Settings;

public sealed class CacheSettings
{
    public const string SectionName = "Cache";

    public bool Enabled { get; init; }

    public bool L2Enabled { get; init; }

    [Range(1, int.MaxValue)]
    public int DefaultTtlSeconds { get; init; } = 300;

    public string ConnectionString { get; init; } = string.Empty;
}
