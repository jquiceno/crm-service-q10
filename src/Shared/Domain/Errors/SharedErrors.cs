namespace Shared.Domain.Errors;

public static class SharedErrors
{
    public static DomainError NotFound(string entityName, Guid id) =>
        new($"{entityName} with id '{id}' was not found.", ErrorType.NotFound);
}
