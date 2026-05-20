namespace Shared.Domain.Errors;

public static class SharedErrors
{
    public static DomainError NotFound(string entityName, Guid id) =>
        new($"{entityName} with id '{id}' was not found.", ErrorType.NotFound);

    public static DomainError UniqueConstraintViolation(string? detail = null) =>
        new(detail is not null
            ? $"A duplicate record already exists ({detail})."
            : "A duplicate record already exists.",
            ErrorType.Conflict);

    public static DomainError ForeignKeyViolation(string? detail = null) =>
        new(detail is not null
            ? $"The operation conflicts with a related record ({detail})."
            : "The operation conflicts with a related record.",
            ErrorType.Conflict);

    public static DomainError NullConstraintViolation(string? columnName = null) =>
        new(columnName is not null
            ? $"Required field '{columnName}' cannot be null."
            : "A required field cannot be null.",
            ErrorType.Validation);

    public static DomainError DataTruncation(string? columnName = null) =>
        new(columnName is not null
            ? $"The value for '{columnName}' exceeds the maximum allowed length."
            : "A value exceeds the maximum allowed length.",
            ErrorType.Validation);

    public static DomainError Deadlock() =>
        new("The operation was aborted due to a deadlock. Please retry.", ErrorType.Internal);

    public static DomainError PersistenceFailure(string detail) =>
        new($"A persistence error occurred: {detail}", ErrorType.Internal);
}
