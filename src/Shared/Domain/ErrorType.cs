namespace Shared.Domain;

public enum ErrorType
{
    None,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Internal
}
