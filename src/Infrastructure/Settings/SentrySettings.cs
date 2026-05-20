namespace Infrastructure.Settings;

public sealed class SentrySettings
{
    public const string SectionName = "Sentry";

    public const string DefaultDeniedHeaders =
        "Authorization,Proxy-Authorization,Cookie,Set-Cookie,X-Api-Key,"
        + "X-Forwarded-For,X-Real-Ip,X-Csrf-Token,X-Xsrf-Token";

    public bool Enabled { get; init; }
    public string Dsn { get; init; } = string.Empty;
    public float TracesSampleRate { get; init; } = 0.2f; // A default value is set in case a value is not configured and to prevent Sentry from being overloaded.
    public string DeniedHeaders { get; init; } = DefaultDeniedHeaders;
}
