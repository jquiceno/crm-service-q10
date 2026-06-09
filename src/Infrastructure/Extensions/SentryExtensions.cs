using Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

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

        var appInfo =
            builder.Configuration.GetSection(AppInfoSettings.SectionName).Get<AppInfoSettings>()
            ?? new AppInfoSettings();

        var deniedHeaders = new HashSet<string>(
            sentrySettings.DeniedHeaders
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase
        );

        var shouldScrubCookies = deniedHeaders.Contains("Cookie")
                              || deniedHeaders.Contains("Set-Cookie");

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentrySettings.Dsn;
            options.Environment = builder.Environment.EnvironmentName;
            options.Release = appInfo.Version;
            options.TracesSampleRate = sentrySettings.TracesSampleRate;
            options.SendDefaultPii = false;

            options.SetBeforeSend((sentryEvent, _) =>
            {
                ScrubRequest(sentryEvent.Request, deniedHeaders, shouldScrubCookies);
                return sentryEvent;
            });

            options.SetBeforeSendTransaction((transaction, _) =>
            {
                ScrubRequest(transaction.Request, deniedHeaders, shouldScrubCookies);
                return transaction;
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
