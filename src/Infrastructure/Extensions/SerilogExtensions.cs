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
                    .Enrich.WithProperty(
                        "environment",
                        context.HostingEnvironment.EnvironmentName.ToLowerInvariant()
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
                                + "Set 'Sentry:Dsn' in appsettings.json or "
                                + "'Sentry__Dsn' as an environment variable. "
                                + "Application startup aborted."
                        );
                    }
                    loggerConfig.WriteTo.Sentry(options =>
                    {
                        options.InitializeSdk = false; // SDK already initialized by SentryExtensions
                        options.MinimumEventLevel = sentrySettings.MinimumEventLevel;
                        options.MinimumBreadcrumbLevel = sentrySettings.MinimumBreadcrumbLevel;
                    });
                }
            }
        );
    }
}
