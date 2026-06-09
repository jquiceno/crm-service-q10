using Shared.Results.Errors;

namespace Shared.Domain.Tenants.Errors;

public static class TenantErrors
{
    public const string Context = "Tenant";

    public static DomainError NotFound(string code) =>
        new($"Tenant with code '{code}' was not found.", ErrorType.NotFound) { Context = Context };

    public static readonly ValidationError CodeRequired =
        new("EntityCode is required.", ErrorType.Validation) { Property = "EntityCode" };

    public static readonly ValidationError CodeTooLong =
        new("EntityCode must not exceed 20 characters.", ErrorType.Validation)
        {
            Property = "EntityCode",
            Attributes = new Dictionary<string, object?> { ["maxLength"] = 20 }
        };
}
