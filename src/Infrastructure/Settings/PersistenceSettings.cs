namespace Infrastructure.Settings;

public sealed class PersistenceSettings
{
    public const string SectionName = "Persistence";

    public bool Enabled { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
}