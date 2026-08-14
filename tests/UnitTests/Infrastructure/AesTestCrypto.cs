using System.Security.Cryptography;
using System.Text;

namespace UnitTests.Infrastructure;

/// <summary>
/// Test-only AES-256-CBC helper that mirrors what the tenant-resolver service produces: the payload is
/// <c>"{ivHex}:{cipherHex}"</c> (hex-encoded IV and ciphertext joined by a colon, PKCS7) and the AES key
/// is the SHA-256 of the passphrase. NOT a secret — fixed passphrase.
/// </summary>
internal static class AesTestCrypto
{
    /// <summary>Deterministic test passphrase; the AES key is its SHA-256 (as the service derives it).</summary>
    public const string Passphrase = "super-secret-passphrase";

    /// <summary>Encrypts <paramref name="plaintext"/> and returns <c>"{ivHex}:{cipherHex}"</c>.</summary>
    public static string Encrypt(string plaintext, string? passphrase = null)
    {
        var key = SHA256.HashData(Encoding.UTF8.GetBytes(passphrase ?? Passphrase));

        using var aes = Aes.Create();
        aes.Key = key;
        var iv = aes.IV; // freshly generated per instance
        var cipher = aes.EncryptCbc(Encoding.UTF8.GetBytes(plaintext), iv, PaddingMode.PKCS7);

        return $"{Convert.ToHexString(iv)}:{Convert.ToHexString(cipher)}";
    }
}
