using Infrastructure.MasterAccess.Http.Tenants;
using Infrastructure.MasterAccess.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.MasterAccess.Extensions;

public static class MasterAccessExtensions
{
    public static IServiceCollection AddMasterAccess(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(TenantResolverServiceSettings.SectionName)
            .Get<TenantResolverServiceSettings>() ?? new TenantResolverServiceSettings();

        services.AddOptions<TenantResolverServiceSettings>()
            .Bind(configuration.GetSection(TenantResolverServiceSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionStringDecryptor, AesConnectionStringDecryptor>();

        services.AddHttpClient<ITenantResolverServiceClient, TenantResolverServiceClient>(client =>
        {
            client.BaseAddress = settings.BaseUri;
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddStandardResilienceHandler(o =>
        {
            o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
            o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds * 2);
            o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(settings.TimeoutSeconds * 2);
        });

        return services;
    }
}
