using AdsChannel.Domain.Aggregates;
using Shared.Results.Errors;

namespace AdsChannel.Domain.Errors;

public static class AdsChannelErrors
{
    public const string Context = "AdsChannel";

    public static DomainError NotFound(int id) =>
        new NotFoundError($"AdsChannel with id '{id}' was not found.");

    public static readonly ValidationError NameRequired =
        new("Name is required.", ErrorType.Validation)
        {
            Property = nameof(AdsChannelAggregate.Name)
        };

    public static readonly ValidationError NameTooLong =
        new("Name cannot exceed 100 characters.", ErrorType.Validation)
        {
            Property = nameof(AdsChannelAggregate.Name),
            Attributes = new Dictionary<string, object?> { ["maxLength"] = 100 }
        };

    public static DomainError NameAlreadyExists(string name) =>
        new ConflictError($"An AdsChannel with name '{name}' already exists.");
}
