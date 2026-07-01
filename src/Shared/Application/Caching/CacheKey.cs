namespace Shared.Application.Caching;

/// <summary>
/// Builds L2 cache keys with the canonical shape <c>ctx:{context}:v1:{resource}:{id}</c>,
/// optionally partitioned by tenant (<c>t:{tenantId}:</c>).
/// </summary>
public sealed class CacheKey
{
    private const string SchemaVersion = "v1";

    private readonly string _context;
    private string? _tenant;

    private CacheKey(string context) => _context = context;

    public static CacheKey For(string context) => new(Segment(context, nameof(context)));

    public CacheKey Tenant(string tenantId)
    {
        _tenant = Segment(tenantId, nameof(tenantId));
        return this;
    }

    public string Resource(string resource, object id) =>
        Build($"{Segment(resource, nameof(resource))}:{id}");

    public string Prefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("'prefix' must be non-empty.", nameof(prefix));

        return Build(prefix);
    }

    private string Build(string tail)
    {
        var tenant = _tenant is null ? string.Empty : $"t:{_tenant}:";
        return $"ctx:{_context}:{SchemaVersion}:{tenant}{tail}";
    }

    private static string Segment(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"'{name}' must be non-empty.", name);
        if (value.Contains(':', StringComparison.Ordinal))
            throw new ArgumentException($"'{name}' must not contain ':'.", name);
        return value;
    }
}
