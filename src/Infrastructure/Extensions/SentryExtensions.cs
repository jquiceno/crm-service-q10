using Infrastructure.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Extensions;

public static class SentryExtensions
{
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

        builder.WebHost.UseSentry(options =>
        {
            options.Dsn = sentrySettings.Dsn;
            options.Environment = builder.Environment.EnvironmentName;
            options.Release = appInfo.Version;
            options.TracesSampleRate = sentrySettings.TracesSampleRate;
            options.SendDefaultPii = false;
        });

        return builder;
    }
}
