using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Domain;

namespace Api.Filters;

internal static class ModelStateValidationAdapter
{
    public static List<ValidationError> Build(ModelStateDictionary modelState)
    {
        var all = modelState.Where(kvp => kvp.Value?.Errors.Count > 0).ToList();
        var hasJsonPathErrors = all.Any(kvp => kvp.Key.StartsWith("$"));

        var relevant = hasJsonPathErrors
            ? all.Where(kvp => kvp.Key.StartsWith("$")).ToList()
            : all;

        var entries = relevant
            .Select(kvp => new Entry(
                ParsePath(kvp.Key),
                kvp.Value!.Errors
                    .Select(e => ExtractMessage(e, kvp.Key.StartsWith("$")))
                    .ToList()))
            .ToList();

        return BuildLevel(entries);
    }

    private static List<ValidationError> BuildLevel(IList<Entry> entries)
    {
        var result = new List<ValidationError>();

        foreach (var group in entries.GroupBy(e => e.Path[0]))
        {
            var direct = group.Where(e => e.Path.Count == 1).SelectMany(e => e.Messages).ToList();
            var deeper = group.Where(e => e.Path.Count > 1)
                              .Select(e => new Entry(e.Path.Skip(1).ToList(), e.Messages))
                              .ToList();

            if (deeper.Count > 0)
            {
                var message = direct.Count > 0 ? direct[0] : $"{group.Key} is invalid.";
                result.Add(new ValidationError(message, ErrorType.Validation)
                {
                    Property = group.Key,
                    Children = BuildLevel(deeper)
                });
            }
            else
            {
                foreach (var msg in direct)
                    result.Add(new ValidationError(msg, ErrorType.Validation) { Property = group.Key });
            }
        }

        return result;
    }

    private static List<string> ParsePath(string key)
    {
        if (string.IsNullOrEmpty(key) || key == "$")
            return ["input"];

        var stripped = key.StartsWith("$.") ? key[2..] : key;
        return stripped.Split('.')
                       .Select(s => char.ToLowerInvariant(s[0]) + s[1..])
                       .ToList();
    }

    private static string ExtractMessage(ModelError error, bool isJsonPath) =>
        isJsonPath
            ? "Invalid JSON format."
            : !string.IsNullOrEmpty(error.ErrorMessage)
                ? error.ErrorMessage
                : "Invalid value.";

    private record Entry(List<string> Path, List<string> Messages);
}
