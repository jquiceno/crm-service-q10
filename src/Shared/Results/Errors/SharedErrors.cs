namespace Shared.Results.Errors;

public static class SharedErrors
{
    public static NotFoundError NotFound(string entityName, object id) =>
        new($"{entityName} with id '{id}' was not found.");
}
