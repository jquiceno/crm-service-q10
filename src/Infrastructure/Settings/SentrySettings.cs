namespace Infrastructure.Settings;

public sealed class SentrySettings
{
    public const string SectionName = "Sentry";

    public bool Enabled { get; init; }
    public string Dsn { get; init; } = string.Empty;
}
