namespace Shared.Domain.Errors;

public static class SharedErrors
{
    public static DomainError NotFound(string entityName, object id) =>
        new($"{entityName} with id '{id}' was not found.", ErrorType.NotFound);
}
