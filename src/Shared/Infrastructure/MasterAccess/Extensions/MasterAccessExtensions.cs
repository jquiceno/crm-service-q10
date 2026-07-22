using Infrastructure.MasterAccess.Http.Tenants;
using Infrastructure.MasterAccess.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Infrastructure.MasterAccess.Extensions;

public static class MasterAccessExtensions
{
    public static IServiceCollection AddMasterAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TenantResolverServiceSettings>()
            .Bind(configuration.GetSection(TenantResolverServiceSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionStringDecryptor, AesConnectionStringDecryptor>();

        services.AddHttpClient<ITenantResolverServiceClient, TenantResolverServiceClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<TenantResolverServiceSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
