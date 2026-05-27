using Shared.Result.Errors;
using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Shared.Presentation.Filters;

public static class JsonTypeScanner
{
    private static readonly ConcurrentDictionary<Type, PropertyMeta[]> PropertyCache = new();
    private const int MaxDepth = 32;

    public static IReadOnlyList<ValidationError> Scan(JsonElement root, Type type, int depth = 0)
    {
        if (depth >= MaxDepth || root.ValueKind != JsonValueKind.Object)
            return [];

        var errors = new List<ValidationError>();
        var props = PropertyCache.GetOrAdd(type, BuildPropertyMeta);

        var jsonProps = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var jp in root.EnumerateObject())
            jsonProps[jp.Name] = jp.Value;

        foreach (var meta in props)
        {
            if (!jsonProps.TryGetValue(meta.JsonName, out var jsonProp)) continue;

            if (jsonProp.ValueKind == JsonValueKind.Null) continue;

            if (!IsCompatible(jsonProp.ValueKind, meta.TargetType))
            {
                errors.Add(new ValidationError(BuildMessage(meta.TargetType), ErrorType.Validation)
                {
                    Property = meta.CamelCaseName,
                    Value = ExtractValue(jsonProp)
                });
            }
            else if (jsonProp.ValueKind == JsonValueKind.Object && IsComplexType(meta.TargetType))
            {
                var children = Scan(jsonProp, meta.TargetType, depth + 1);
                if (children.Count > 0)
                    errors.Add(new ValidationError("Validation failed", ErrorType.Validation)
                    {
                        Property = meta.CamelCaseName,
                        Children = children
                    });
            }
        }

        return errors;
    }

    private static PropertyMeta[] BuildPropertyMeta(Type type) =>
        type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => new PropertyMeta(
                GetJsonName(p),
                ToCamelCase(p.Name),
                Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType))
            .ToArray();

    private static bool IsCompatible(JsonValueKind kind, Type target) => kind switch
    {
        JsonValueKind.String =>
            target == typeof(string) || target == typeof(char) ||
            target == typeof(Guid) ||
            target == typeof(DateTime) || target == typeof(DateTimeOffset) ||
            target == typeof(DateOnly) || target == typeof(TimeOnly) ||
            target == typeof(TimeSpan) || target == typeof(Uri) ||
            target.IsEnum,
        JsonValueKind.Number => IsNumeric(target) || target.IsEnum,
        JsonValueKind.True or JsonValueKind.False => target == typeof(bool),
        JsonValueKind.Array =>
            target != typeof(string) && typeof(IEnumerable).IsAssignableFrom(target),
        JsonValueKind.Object => IsComplexType(target),
        _ => false
    };

    private static bool IsNumeric(Type t) =>
        t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte) ||
        t == typeof(uint) || t == typeof(ulong) || t == typeof(ushort) || t == typeof(sbyte) ||
        t == typeof(float) || t == typeof(double) || t == typeof(decimal) ||
        t == typeof(Int128) || t == typeof(UInt128) || t == typeof(Half);

    private static bool IsComplexType(Type t) =>
        t.IsClass &&
        !t.IsPrimitive &&
        t != typeof(string) && t != typeof(object) && t != typeof(Uri) &&
        !IsNumeric(t) &&
        !typeof(IEnumerable).IsAssignableFrom(t);

    private static object? ExtractValue(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString(),
        JsonValueKind.Number when element.TryGetInt64(out var l) => l,
        JsonValueKind.Number when element.TryGetDecimal(out var d) => d,
        JsonValueKind.Number => element.GetRawText(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Array or JsonValueKind.Object => element.Clone(),
        _ => null
    };

    private static string BuildMessage(Type targetType) =>
        $"Expected {GetFriendlyTypeName(targetType)}.";

    private static string GetFriendlyTypeName(Type t)
    {
        if (t == typeof(string) || t == typeof(char)) return "a string";
        if (IsNumeric(t)) return "a number";
        if (t == typeof(bool)) return "a boolean";
        if (t == typeof(Guid)) return "a GUID string";
        if (t == typeof(DateTime) || t == typeof(DateTimeOffset)) return "a date-time string";
        if (t == typeof(DateOnly)) return "a date string";
        if (t == typeof(TimeOnly) || t == typeof(TimeSpan)) return "a time string";
        if (t == typeof(Uri)) return "a URI string";
        if (t.IsEnum) return "a valid enum value";
        return "a valid value";
    }

    private static string GetJsonName(PropertyInfo prop)
    {
        var attr = prop.GetCustomAttribute<JsonPropertyNameAttribute>();
        return attr?.Name ?? ToCamelCase(prop.Name);
    }

    private static string ToCamelCase(string s) =>
        s.Length == 0 ? s : char.ToLowerInvariant(s[0]) + s[1..];

    private sealed record PropertyMeta(string JsonName, string CamelCaseName, Type TargetType);
}
