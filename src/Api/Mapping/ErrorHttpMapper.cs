using System.Net;
using Shared.Domain;

namespace Api.Mapping;

public static class ErrorHttpMapper
{
    public static HttpStatusCode ToHttpStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.Validation => HttpStatusCode.BadRequest,
        ErrorType.NotFound => HttpStatusCode.NotFound,
        ErrorType.Conflict => HttpStatusCode.Conflict,
        ErrorType.Unauthorized => HttpStatusCode.Unauthorized,
        ErrorType.Forbidden => HttpStatusCode.Forbidden,
        ErrorType.Internal => HttpStatusCode.InternalServerError,
        _ => HttpStatusCode.InternalServerError
    };
}
