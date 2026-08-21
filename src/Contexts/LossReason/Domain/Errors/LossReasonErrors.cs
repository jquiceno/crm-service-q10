using LossReason.Domain.Aggregates;
using Shared.Results.Errors;

namespace LossReason.Domain.Errors;

public static class LossReasonErrors
{
    public const string Context = "LossReason";

    public static readonly ValidationError NameRequired =
        new("Loss reason name is required.", ErrorType.Validation)
        {
            Property = nameof(LossReasonAggregate.Name)
        };

    public static readonly ValidationError NameTooLong =
        new($"Loss reason name must not exceed {LossReasonAggregate.NameMaxLength} characters.",
            ErrorType.Validation)
        {
            Property = nameof(LossReasonAggregate.Name),
            Attributes = new Dictionary<string, object?> { ["max"] = LossReasonAggregate.NameMaxLength }
        };

    public static DomainError NotFound(int id) =>
        new($"Loss reason with id '{id}' was not found.", ErrorType.NotFound);

    public static DomainError InUse(int id) =>
        new($"Loss reason with id '{id}' is assigned to at least one deal and cannot be deleted.",
            ErrorType.Conflict);
}
