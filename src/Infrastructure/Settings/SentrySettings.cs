using Serilog.Events;

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
    public LogEventLevel MinimumEventLevel { get; init; } = LogEventLevel.Error;
    public LogEventLevel MinimumBreadcrumbLevel { get; init; } = LogEventLevel.Warning;

    /// <summary>
    /// Deployment tier reported to Sentry: dev, qa or prod, always lowercase. Comes from the
    /// platform-wide SENTRY_ENVIRONMENT variable so every service reports the same literal
    /// regardless of stack.
    /// </summary>
    /// <remarks>
    /// Deliberately NOT derived from ASPNETCORE_ENVIRONMENT. That one is a runtime mode — it picks
    /// which appsettings load and whether developer exception pages show — and it only has two
    /// useful values, so qa and prod would both report "Production" and become indistinguishable.
    /// </remarks>
    public string Environment { get; init; } = string.Empty;

    /// <summary>
    /// Minimum level forwarded to the Logs product (Explore &gt; Logs). Separate from
    /// <see cref="MinimumEventLevel"/>, which controls what becomes an Issue.
    /// </summary>
    public LogEventLevel MinimumLogLevel { get; init; } = LogEventLevel.Warning;
}
