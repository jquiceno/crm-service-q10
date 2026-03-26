using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;

namespace Infrastructure.Extensions;

public static class SerilogExtensions
{
    public static IHostBuilder AddSerilog(
        this IHostBuilder hostBuilder,
        IConfiguration configuration)
    {
        var sentrySettings = configuration
            .GetSection(SentrySettings.SectionName)
            .Get<SentrySettings>() ?? new SentrySettings();

        return hostBuilder.UseSerilog((context, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            if (sentrySettings.Enabled)
            {
                if (string.IsNullOrWhiteSpace(sentrySettings.Dsn))
                {
                    throw new InvalidOperationException(
                        "Critical Error: SENTRY is enabled but Dsn is missing. "
                        + "Set 'Sentry:Dsn' in appsettings.json or "
                        + "'Sentry__Dsn' as an environment variable. "
                        + "Application startup aborted.");
                }

                loggerConfig.WriteTo.Sentry(options =>
                {
                    options.Dsn = sentrySettings.Dsn;
                    options.MinimumEventLevel = LogEventLevel.Error;
                    options.MinimumBreadcrumbLevel = LogEventLevel.Warning;
                });
            }
        });
    }
}
