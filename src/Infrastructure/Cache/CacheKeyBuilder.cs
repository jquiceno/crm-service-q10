using System.Security.Cryptography;
using System.Text;

namespace Infrastructure.Cache;

public static class CacheKeyBuilder
{
    /// <summary>
    /// Builds a cache key in the format: {prefix}:{route}:{queryHash}:{tenant}:{locale}
    /// </summary>
    public static string Build(
        string prefix,
        string route,
        string? queryString,
        string? tenant,
        string? locale)
    {
        var queryHash = HashQueryString(queryString);
        var tenantPart = Sanitize(tenant);
        var localePart = Sanitize(locale);

        return $"{prefix}:{route}:{queryHash}:{tenantPart}:{localePart}";
    }

    /// <summary>
    /// Builds the prefix used for pattern-based cache invalidation.
    /// Matches all keys for a given route regardless of query, tenant, or locale.
    /// </summary>
    public static string BuildRoutePrefix(string prefix, string route)
        => $"{prefix}:{route}:";

    private static string HashQueryString(string? queryString)
    {
        if (string.IsNullOrEmpty(queryString))
            return "_";

        // Sort params so ?a=1&b=2 and ?b=2&a=1 produce the same key.
        var normalized = queryString.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.ToLowerInvariant())
            .Order()
            .Aggregate((a, b) => $"{a}&{b}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        // 8 bytes → 16 hex chars — enough entropy for a query fingerprint.
        return Convert.ToHexString(hash[..8]).ToLowerInvariant();
    }

    private static string Sanitize(string? value)
        => string.IsNullOrWhiteSpace(value) ? "_" : value.Trim().ToLowerInvariant();
}
