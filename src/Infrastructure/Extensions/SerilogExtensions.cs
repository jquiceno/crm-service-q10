using Infrastructure.Logging;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace Infrastructure.Extensions;

public static class SerilogExtensions
{
    public static IHostBuilder AddSerilog(
        this IHostBuilder hostBuilder,
        IConfiguration configuration
    )
    {
        var sentrySettings =
            configuration.GetSection(SentrySettings.SectionName).Get<SentrySettings>()
            ?? new SentrySettings();

        return hostBuilder.UseSerilog(
            (context, loggerConfig) =>
            {
                var serviceInfo =
                    configuration.GetSection(ServiceInfoSettings.SectionName).Get<ServiceInfoSettings>()
                    ?? throw new InvalidOperationException(
                        $"Missing required configuration section '{ServiceInfoSettings.SectionName}'. "
                        + "Ensure 'ServiceInfo:Name' and 'ServiceInfo:Version' are set in appsettings.json.");

                loggerConfig
                    .ReadFrom.Configuration(configuration)
                    .Enrich.FromLogContext()
                    .Enrich.With<ActivityEnricher>()
                    .Enrich.WithProperty("service", serviceInfo.Name)
                    // Same resolved tier the SDK and the OTel resource report, so a log, a span and
                    // an error from one request all agree on the environment.
                    .Enrich.WithProperty(
                        "environment",
                        SentryExtensions.ResolveEnvironment(
                            sentrySettings.Environment,
                            context.HostingEnvironment.EnvironmentName
                        )
                    )
                    .Enrich.WithProperty("version", serviceInfo.Version);

                if (context.HostingEnvironment.IsDevelopment())
                {
                    loggerConfig.WriteTo.Console();
                }
                else
                {
                    loggerConfig.WriteTo.Console(new FlatJsonFormatter());
                }

                if (sentrySettings.Enabled)
                {
                    if (string.IsNullOrWhiteSpace(sentrySettings.Dsn))
                    {
                        throw new InvalidOperationException(
                            "Critical Error: SENTRY is enabled but Dsn is missing. "
                                + "Set the 'SENTRY_DSN' environment variable (platform-shared secret) "
                                + "or 'Sentry:Dsn' in appsettings.json. "
                                + "Application startup aborted."
                        );
                    }
                    loggerConfig.WriteTo.Sentry(options =>
                    {
                        options.InitializeSdk = false; // SDK already initialized by SentryExtensions
                        options.MinimumEventLevel = sentrySettings.MinimumEventLevel;
                        options.MinimumBreadcrumbLevel = sentrySettings.MinimumBreadcrumbLevel;

                        // Forwards Serilog events to the Logs product as structured logs. The level
                        // cut lives in SetBeforeSendLog on the SDK options, next to EnableLogs.
#pragma warning disable SENTRY0001 // Structured logging is still marked experimental in the SDK.
                        options.EnableLogs = true;
#pragma warning restore SENTRY0001
                    });
                }
            }
        );
    }
}
