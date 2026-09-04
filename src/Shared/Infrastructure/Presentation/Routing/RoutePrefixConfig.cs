using Microsoft.Extensions.Configuration;

namespace Shared.Presentation.Routing;

// Single source for the service route prefix (config key "RoutePrefix"): the controller convention,
// the health/OpenAPI maps and the tenant exclusions all normalize through here.
public static class RoutePrefixConfig
{
    public const string ConfigKey = "RoutePrefix";

    public static string Normalize(string? prefix) => (prefix ?? string.Empty).Trim().Trim('/');

    public static string BasePath(string? prefix)
    {
        var normalized = Normalize(prefix);
        return normalized.Length == 0 ? string.Empty : $"/{normalized}";
    }

    public static string GetRoutePrefix(this IConfiguration configuration) => Normalize(configuration[ConfigKey]);
}
