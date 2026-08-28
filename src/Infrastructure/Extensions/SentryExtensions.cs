using Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog.Events;

namespace Infrastructure.Extensions;

public static class SentryExtensions
{
    private const string FilteredValue = "[Filtered]";

    /// <summary>
    /// Language-agnostic DSN variable published by the platform-shared secret.
    /// </summary>
    public const string SharedDsnVariable = "SENTRY_DSN";

    /// <summary>
    /// Language-agnostic variable holding the deployment tier (dev, qa, prod), published once per
    /// overlay so every service reports the same literal regardless of stack.
    /// </summary>
    public const string SharedEnvironmentVariable = "SENTRY_ENVIRONMENT";

    /// <summary>
    /// Resolves the tier reported to Sentry, always lowercase. Falls back to the host environment
    /// name only for local runs, where no overlay supplies the variable.
    /// </summary>
    public static string ResolveEnvironment(string? configured, string hostEnvironmentName) =>
        (string.IsNullOrWhiteSpace(configured) ? hostEnvironmentName : configured)
            .Trim()
            .ToLowerInvariant();

    public static WebApplicationBuilder AddSentry(this WebApplicationBuilder builder)
    {
        // The platform-shared secret publishes the DSN under the language-agnostic
        // name SENTRY_DSN; bridge it to the .NET config key before binding.
        var sharedDsn = builder.Configuration[SharedDsnVariable];
        if (!string.IsNullOrWhiteSpace(sharedDsn))
            builder.Configuration[$"{SentrySettings.SectionName}:Dsn"] = sharedDsn;

        // Same bridge for the tier. Deliberately NOT ASPNETCORE_ENVIRONMENT: that one is a runtime
        // mode with two useful values, so qa and prod would both report "Production" and stop being
        // distinguishable in Sentry.
        var sharedEnvironment = builder.Configuration[SharedEnvironmentVariable];
        if (!string.IsNullOrWhiteSpace(sharedEnvironment))
            builder.Configuration[$"{SentrySettings.SectionName}:Environment"] = sharedEnvironment;

        var sentrySettings =
            builder.Configuration.GetSection(SentrySettings.SectionName).Get<SentrySettings>()
            ?? new SentrySettings();

        if (!sentrySettings.Enabled)
            return builder;

        if (string.IsNullOrWhiteSpace(sentrySettings.Dsn))
        {
            throw new InvalidOperationException(
                "Critical Error: SENTRY is enabled but Dsn is missing. "
                    + "Set the 'SENTRY_DSN' environment variable (platform-shared secret) "
                    + "or 'Sentry:Dsn' in appsettings.json. "
                    + "Application startup aborted."
            );
        }

        var serviceInfo =
            builder.Configuration.GetSection(ServiceInfoSettings.SectionName).Get<ServiceInfoSettings>()
            ?? new ServiceInfoSettings();

        var deniedHeaders = new HashSet<string>(
            sentrySettings.DeniedHeaders
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase
        );

        var shouldScrubCookies = deniedHeaders.Contains("Cookie")
                              || deniedHeaders.Contains("Set-Cookie");

        // Sentry ingest host (derived from the DSN). It is excluded from
        // HttpClient instrumentation to avoid auto-instrumenting Sentry's own telemetry transport,
        // which would otherwise generate noisy spans (POST .../envelope/) on every trace.
        var sentryIngestHost = new Uri(sentrySettings.Dsn).Host;

        // Distributed tracing: traces are produced by OpenTelemetry and exported to Sentry
        // via OTLP. This makes Sentry's principal trace the same Activity/W3C trace id that
        // appears in the logs and in the X-Trace-Id header. Sampling is driven by the OTel
        // sampler (ParentBased honors the upstream service's decision for a consistent trace).
        var environment = ResolveEnvironment(sentrySettings.Environment, builder.Environment.EnvironmentName);

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceInfo.Name, serviceVersion: serviceInfo.Version)
                // Spans travel by OTLP, outside the SDK pipeline, so options.Environment below does
                // not reach them: without this attribute every span arrives with no environment and
                // filtering by environment silently misses this service's traces.
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment", environment)]))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(
                    new TraceIdRatioBasedSampler(sentrySettings.TracesSampleRate)))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation(o =>
                    o.FilterHttpRequestMessage = request =>
                        !string.Equals(request.RequestUri?.Host, sentryIngestHost, StringComparison.OrdinalIgnoreCase))
                // Without this the database round-trip is invisible: AspNetCore and HttpClient
                // instrumentation cover the request and the outbound calls, so an endpoint that
                // spends most of its time querying shows a long root span with nothing under it.
                .AddSqlClientInstrumentation()
                // Makes a cache hit legible: without it a served-from-cache request is a single
                // root span with nothing underneath, indistinguishable from work that never ran.
                // Resolves IConnectionMultiplexer from DI, which AddDistributedCache registers.
                .AddRedisInstrumentation()
                .AddSentryOtlpExporter(sentrySettings.Dsn));

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentrySettings.Dsn;
            options.Environment = environment;
            options.Release = serviceInfo.Version;
            options.SendDefaultPii = false;

            // Feeds the Logs product (Explore > Logs). Without it the SDK installs a disabled
            // structured logger and drops every log, which is why this service produced errors and
            // spans but no log stream at all.
#pragma warning disable SENTRY0001 // Structured logging is still marked experimental in the SDK.
            options.EnableLogs = true;

            // The Logs equivalent of MinimumEventLevel: Issues and the log stream are separate
            // destinations and want different thresholds. Returning null drops the log.
            options.SetBeforeSendLog(log =>
                log.Level >= MapLogLevel(sentrySettings.MinimumLogLevel) ? log : null!);
#pragma warning restore SENTRY0001

            // Routes traces via OTLP and makes the SDK read the trace context from the OTel
            // Activity: errors land on the SAME trace as the spans and the logs.
            // Sampling and transactions are no longer handled by the SDK (OTel does), which is
            // why TracesSampleRate and SetBeforeSendTransaction are not set.
            options.UseOtlp();
            options.DisableSentryHttpMessageHandler = true;

            // DeniedHeaders scrubbing: applies to ERROR EVENTS. Traces go via OTLP (outside the
            // SDK pipeline); their spans carry no headers because the ASP.NET Core instrumentation
            // does not capture them by default and redacts the query string.
            options.SetBeforeSend((sentryEvent, _) =>
            {
                ScrubRequest(sentryEvent.Request, deniedHeaders, shouldScrubCookies);
                return sentryEvent;
            });

            options.SetBeforeBreadcrumb((breadcrumb, _) =>
                ScrubBreadcrumb(breadcrumb, deniedHeaders));
        });

        return builder;
    }

    /// <summary>
    /// Bridges Serilog's level scale onto Sentry's. The two differ in two names only — Verbose maps
    /// to Trace and Information to Info — so the settings stay expressed in Serilog terms like the
    /// neighbouring MinimumEventLevel and MinimumBreadcrumbLevel.
    /// </summary>
    private static SentryLogLevel MapLogLevel(LogEventLevel level) => level switch
    {
        LogEventLevel.Verbose => SentryLogLevel.Trace,
        LogEventLevel.Debug => SentryLogLevel.Debug,
        LogEventLevel.Information => SentryLogLevel.Info,
        LogEventLevel.Warning => SentryLogLevel.Warning,
        LogEventLevel.Error => SentryLogLevel.Error,
        _ => SentryLogLevel.Fatal
    };

    private static void ScrubRequest(
        SentryRequest? request,
        HashSet<string> deniedHeaders,
        bool shouldScrubCookies)
    {
        if (request?.Headers is not { Count: > 0 } headers)
            return;

        foreach (var key in headers.Keys.ToList())
        {
            if (deniedHeaders.Contains(key))
                headers[key] = FilteredValue;
        }

        if (shouldScrubCookies && !string.IsNullOrEmpty(request.Cookies))
        {
            request.Cookies = FilteredValue;
        }
    }

    private static Breadcrumb ScrubBreadcrumb(Breadcrumb breadcrumb, HashSet<string> deniedHeaders)
    {
        if (breadcrumb.Data is not { Count: > 0 } data)
            return breadcrumb;

        var hasMatch = false;

        foreach (var key in data.Keys)
        {
            if (deniedHeaders.Contains(key))
            {
                hasMatch = true;
                break;
            }
        }

        if (!hasMatch)
            return breadcrumb;

        var scrubbedData = new Dictionary<string, string>(data.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (key, value) in data)
        {
            scrubbedData[key] = deniedHeaders.Contains(key) ? FilteredValue : value;
        }

        return new Breadcrumb(
            message: breadcrumb.Message ?? string.Empty,
            type: breadcrumb.Type ?? string.Empty,
            data: scrubbedData,
            category: breadcrumb.Category,
            level: breadcrumb.Level
        );
    }
}
