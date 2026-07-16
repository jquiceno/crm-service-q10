using Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Infrastructure.Extensions;

public static class SentryExtensions
{
    private const string FilteredValue = "[Filtered]";

    public static WebApplicationBuilder AddSentry(this WebApplicationBuilder builder)
    {
        var sentrySettings =
            builder.Configuration.GetSection(SentrySettings.SectionName).Get<SentrySettings>()
            ?? new SentrySettings();

        if (!sentrySettings.Enabled)
            return builder;

        if (string.IsNullOrWhiteSpace(sentrySettings.Dsn))
        {
            throw new InvalidOperationException(
                "Critical Error: SENTRY is enabled but Dsn is missing. "
                    + "Set 'Sentry:Dsn' in appsettings.json or "
                    + "'Sentry__Dsn' as an environment variable. "
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
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(serviceName: serviceInfo.Name, serviceVersion: serviceInfo.Version))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(
                    new TraceIdRatioBasedSampler(sentrySettings.TracesSampleRate)))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation(o =>
                    o.FilterHttpRequestMessage = request =>
                        !string.Equals(request.RequestUri?.Host, sentryIngestHost, StringComparison.OrdinalIgnoreCase))
                .AddSentryOtlpExporter(sentrySettings.Dsn));

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentrySettings.Dsn;
            options.Environment = builder.Environment.EnvironmentName;
            options.Release = serviceInfo.Version;
            options.SendDefaultPii = false;

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
