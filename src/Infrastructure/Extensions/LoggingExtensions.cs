using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Sentry.Serilog;
using Serilog;
using Serilog.Events;

namespace Infrastructure.Extensions;

public static class LoggingExtensions
{
    public static IHostBuilder AddLoggingConfiguration(
        this IHostBuilder hostBuilder,
        IConfiguration configuration)
    {
        return hostBuilder.UseSerilog((context, loggerConfig) =>
        {
            loggerConfig
                .ReadFrom.Configuration(configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console();

            var sentryDsn = configuration["Sentry:Dsn"];

            if (!string.IsNullOrWhiteSpace(sentryDsn))
            {
                loggerConfig.WriteTo.Sentry(options =>
                {
                    options.Dsn = sentryDsn;
                    options.MinimumEventLevel = LogEventLevel.Error;
                    options.MinimumBreadcrumbLevel = LogEventLevel.Warning;
                });
            }
        });
    }
}
