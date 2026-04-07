using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Extensions;

public static class KeyVaultExtensions
{
    public static ConfigurationManager AddAzureKeyVault(this ConfigurationManager configuration)
    {
        var enabled = configuration.GetValue<bool>("ENABLE_SECRETS_VAULT");

        if (!enabled)
        {
            // Console.WriteLine is intentional here: Serilog is not yet configured during the configuration-building phase.
            Console.WriteLine("[KeyVault] ENABLE_SECRETS_VAULT is not enabled. Skipping Azure Key Vault configuration.");
            return configuration;
        }

        var keyVaultEndpoint = configuration["AZURE_KEYVAULT_ENDPOINT"];

        if (string.IsNullOrWhiteSpace(keyVaultEndpoint))
        {
            Console.WriteLine("[KeyVault] WARNING: ENABLE_SECRETS_VAULT is true but AZURE_KEYVAULT_ENDPOINT is not set. Skipping Azure Key Vault configuration.");
            return configuration;
        }

        if (!Uri.TryCreate(keyVaultEndpoint, UriKind.Absolute, out var keyVaultUri))
        {
            Console.WriteLine($"[KeyVault] WARNING: AZURE_KEYVAULT_ENDPOINT value '{keyVaultEndpoint}' is not a valid URI. Skipping Azure Key Vault configuration.");
            return configuration;
        }

        configuration.AddAzureKeyVault(keyVaultUri, new DefaultAzureCredential());

        return configuration;
    }
}
