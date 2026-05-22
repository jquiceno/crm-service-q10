using Azure.Identity;
using Infrastructure.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Infrastructure.Extensions;

public static class KeyVaultExtensions
{
    public static void AddAzureKeyVault(this ConfigurationManager configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var tempConfig = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new KeyVaultSettings();
        tempConfig.GetSection("Azure:KeyVault").Bind(options);

        if (string.IsNullOrEmpty(options.Endpoint))
            return;

        if (!Uri.TryCreate(options.Endpoint, UriKind.Absolute, out var keyVaultUri))
            return;

        var credential = new ClientSecretCredential(
            options.TenantId,
            options.ClientId,
            options.ClientSecret);

        configuration.AddAzureKeyVault(keyVaultUri, credential);
    }
}