using FluentValidation.Results;
using Infrastructure.Validation.FluentValidation;
using Shared.Application;
using Shared.Application.Ports;
using Shared.Domain.Errors;
using Shared.Domain.Result;
using System.Collections;
using System.Reflection;

namespace Infrastructure.Adapters.Validation;

public sealed class FluentRequestValidationAdapter<T>(IStructuralValidator<T> validator) : IRequestValidatorPort<T>
{
    // Carries the original failure untouched so no fields (ErrorCode, Severity, etc.) are lost
    // when stripping path segments during recursive tree construction.
    private readonly record struct PendingFailure(ValidationFailure Original, string RemainingPath);

    public async Task<Result> ValidateAsync(T input, CancellationToken cancellationToken = default)
    {
        var result = await validator.ValidateAsync(input, cancellationToken);

        if (result.IsValid)
            return Result.Success();

        var pending = result.Errors
            .Select(f => new PendingFailure(f, f.PropertyName))
            .ToList();
        var errors = BuildErrors(pending, input);
        return ApplicationErrors.ValidationFailed(errors, context: string.Empty, origin: string.Empty);
    }

    // Recursively maps a flat list of FluentValidation failures into a tree of ValidationErrors.
    // FluentValidation reports all failures as dot-separated paths (e.g. "Address.Street.ZipCode"),
    // but the domain model expects a nested structure that mirrors the object graph.
    private static List<ValidationError> BuildErrors(IList<PendingFailure> pending, object? source)
    {
        var errors = new List<ValidationError>();

        // Group by the first path segment so that all failures under the same property
        // (e.g. "Address.Street" and "Address.City") are handled together as one parent node.
        foreach (var group in pending.GroupBy(p => RootProperty(p.RemainingPath)))
        {
            // A dot in the remaining path means the failure belongs to a deeper level.
            var nested = group.Where(p => p.RemainingPath.Contains('.')).ToList();
            var flat = group.Where(p => !p.RemainingPath.Contains('.')).ToList();

            if (nested.Count > 0)
            {
                // When the same root has both a direct failure ("Address is required") and nested
                // failures ("Address.Street is required"), we emit a single parent node instead of
                // two separate nodes with the same Property — which would be ambiguous for consumers.
                var firstFlat = flat.Count > 0 ? flat[0].Original : null;

                // CustomState holds the domain-typed error set via .WithState() in the validator,
                // which carries the ErrorType and Attributes defined in the domain error catalog.
                var staticError = firstFlat?.CustomState as ValidationError;

                // Prefer the real failure message over the generic fallback so the parent node
                // conveys the actual rule that was violated when a direct rule exists.
                var message = firstFlat?.ErrorMessage ?? $"{group.Key} is invalid.";
                var errorType = staticError?.Type ?? ErrorType.Validation;

                // Resolve the intermediate object (e.g. the Address instance) to populate Value
                // on the parent node. AttemptedValue on nested failures holds the leaf value,
                // not the parent object, so reflection on the source is necessary here.
                var childSource = GetPropertyValue(source, group.Key);

                // Strip the root segment before recursing so each level only sees its own
                // sub-paths (e.g. "Address.Street" becomes "Street" at the next level).
                var childPending = nested
                    .Select(p => new PendingFailure(p.Original, ChildProperty(p.RemainingPath)))
                    .ToList();

                errors.Add(new ValidationError(message, errorType)
                {
                    Property = group.Key,
                    Value = childSource,
                    Attributes = staticError?.Attributes,
                    Children = BuildErrors(childPending, childSource)
                });
            }
            else
            {
                foreach (var p in flat)
                    errors.Add(ToValidationError(p.Original, group.Key));
            }
        }

        return errors;
    }

    private static ValidationError ToValidationError(ValidationFailure f, string property)
    {
        var staticError = f.CustomState as ValidationError;
        return new ValidationError(f.ErrorMessage, staticError?.Type ?? ErrorType.Validation)
        {
            Property = property,
            Value = f.AttemptedValue,
            Attributes = staticError?.Attributes
        };
    }

    // Returns everything before the first dot: "Address.Street" → "Address".
    private static string RootProperty(string propertyName)
    {
        var dot = propertyName.IndexOf('.');
        return dot < 0 ? propertyName : propertyName[..dot];
    }

    // Returns everything after the first dot: "Address.Street" → "Street".
    private static string ChildProperty(string propertyName)
    {
        var dot = propertyName.IndexOf('.');
        return dot < 0 ? propertyName : propertyName[(dot + 1)..];
    }

    // Navigates the object graph by property name, including indexed access for collections.
    // FluentValidation produces names like "Items[0].Name"; plain reflection cannot resolve
    // "Items[0]" as a property name, so bracket notation is handled explicitly.
    private static object? GetPropertyValue(object? source, string propertyName)
    {
        if (source is null || string.IsNullOrEmpty(propertyName)) return null;

        var bracketStart = propertyName.IndexOf('[');
        if (bracketStart >= 0)
        {
            var bracketEnd = propertyName.IndexOf(']', bracketStart);
            if (bracketEnd < 0 || !int.TryParse(
                    propertyName.AsSpan(bracketStart + 1, bracketEnd - bracketStart - 1), out var index))
                return null;

            // Resolve the collection first (e.g. "Items"), then access the element by index.
            var collection = GetPropertyValue(source, propertyName[..bracketStart]);

            // IList is preferred because it supports O(1) index access and bounds checking.
            // IEnumerable is the fallback for read-only sequences that don't implement IList.
            if (collection is IList list)
                return index < list.Count ? list[index] : null;

            if (collection is IEnumerable enumerable)
                return enumerable.Cast<object>().ElementAtOrDefault(index);

            return null;
        }

        var prop = source.GetType().GetProperty(propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
        return prop?.GetValue(source);
    }

    async Task<Result> IRequestValidatorPort.ValidateAsync(object input, CancellationToken cancellationToken)
        => await ValidateAsync((T)input, cancellationToken);
}
