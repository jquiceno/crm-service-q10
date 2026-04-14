namespace Infrastructure.Settings;

public sealed class SentrySettings
{
    public const string SectionName = "Sentry";

    public bool Enabled { get; init; }
    public string Dsn { get; init; } = string.Empty;
    public float TracesSampleRate { get; init; } = 0.2f; // A default value is set in case a value is not configured and to prevent Sentry from being overloaded.
}
