using Infrastructure.Logging;

namespace Api.Middleware;

public static class HttpContextLogExtensions
{
    private const string LogPropertiesKey = "__log_properties__";

    public static IDisposable PushLogProperties(
        this HttpContext context,
        IReadOnlyDictionary<string, object?> properties)
    {
        // Accumulate all pushed properties so http.request.completed includes
        // properties from every PushLogProperties call in the same request.
        if (context.Items[LogPropertiesKey] is not Dictionary<string, object?> accumulated)
        {
            accumulated = new Dictionary<string, object?>();
            context.Items[LogPropertiesKey] = accumulated;
        }

        foreach (var (key, value) in properties)
            accumulated[key] = value;

        return accumulated.PushProperties();
    }

    internal static IReadOnlyDictionary<string, object?>? GetLogProperties(this HttpContext context) =>
        context.Items.TryGetValue(LogPropertiesKey, out var value)
            ? value as IReadOnlyDictionary<string, object?>
            : null;
}
