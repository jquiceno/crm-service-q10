using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Infrastructure.MasterAccess.Http.Tenants;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.MasterAccess.Security;

public sealed class AesConnectionStringDecryptor(
    IOptions<TenantInfoClientSettings> options,
    ILoggerPort<AesConnectionStringDecryptor> logger) : IConnectionStringDecryptor
{
    private const string Context = "TenantInfo";
    private const int IvSizeBytes = 16; // AES block size

    // Derived once: SHA-256 of the passphrase always yields the 32 bytes AES-256 requires.
    private readonly byte[] _key = SHA256.HashData(Encoding.UTF8.GetBytes(options.Value.ConnectionStringKey));

    public Result<string> Decrypt(string cipherText)
    {
        if (string.IsNullOrWhiteSpace(cipherText))
            return new InternalError("The encrypted connection string is empty.") { Context = Context };

        try
        {
            // Layout produced by the service: "{ivHex}:{cipherHex}".
            var separator = cipherText.IndexOf(':');
            if (separator <= 0 || separator == cipherText.Length - 1)
                return new InternalError("The encrypted connection string is malformed.") { Context = Context };

            var iv = Convert.FromHexString(cipherText.AsSpan(0, separator));
            var cipher = Convert.FromHexString(cipherText.AsSpan(separator + 1));

            if (iv.Length != IvSizeBytes)
                return new InternalError("The encrypted connection string is malformed.") { Context = Context };

            using var aes = Aes.Create();
            aes.Key = _key;
            var plain = aes.DecryptCbc(cipher, iv, PaddingMode.PKCS7);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            logger.Error(ex, "Failed to decrypt the tenant connection string.");
            return new InternalError("Failed to decrypt the tenant connection string.") { Context = Context };
        }
    }
}
