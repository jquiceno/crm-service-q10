using System.Net;
using Api.Responses;
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
        ErrorType.DomainError => HttpStatusCode.BadRequest,
        _ => HttpStatusCode.InternalServerError
    };

    public static ErrorDetailDto[] ToErrorDetailDtos(IReadOnlyList<ErrorDetail> details) =>
        details
            .Select(d => new ErrorDetailDto(ToCamelCase(d.Property), d.Value, d.Errors, d.Attributes))
            .ToArray();

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}