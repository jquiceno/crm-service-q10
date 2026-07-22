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
        services.AddOptions<TenantInfoClientSettings>()
            .Bind(configuration.GetSection(TenantInfoClientSettings.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IConnectionStringDecryptor, AesConnectionStringDecryptor>();

        services.AddHttpClient<ITenantInfoClient, TenantInfoClient>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<TenantInfoClientSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
            client.Timeout = Timeout.InfiniteTimeSpan;
        })
        .AddStandardResilienceHandler();

        return services;
    }
}
