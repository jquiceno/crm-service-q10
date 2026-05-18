using System.Collections.Frozen;
using System.Net;
using Api.Responses;
using Shared.Domain.Errors;

namespace Api.Mapping;

public static class ErrorHttpMapper
{
    private const string ErrorCodePrefix = "HTTP";

    private static readonly FrozenDictionary<ErrorType, string> ErrorTypeNames =
        new Dictionary<ErrorType, string>
        {
            [ErrorType.None]         = "NONE",
            [ErrorType.Validation]   = "VALIDATION",
            [ErrorType.NotFound]     = "NOT_FOUND",
            [ErrorType.Conflict]     = "CONFLICT",
            [ErrorType.Unauthorized] = "UNAUTHORIZED",
            [ErrorType.Forbidden]    = "FORBIDDEN",
            [ErrorType.Internal]     = "INTERNAL",
            [ErrorType.DomainError]  = "DOMAIN_VALIDATION",
        }.ToFrozenDictionary();

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

    public static string ToErrorTypeName(ErrorType errorType) =>
        ErrorTypeNames.TryGetValue(errorType, out var name) ? name : "INTERNAL";

    public static string ToErrorCode(ErrorType errorType) =>
        $"{ErrorCodePrefix}.{ToErrorTypeName(errorType)}";

    public static ErrorDetailDto[] ToErrorDetailDtos(IReadOnlyList<ErrorDetail> details) =>
        details.Select(ToErrorDetailDto).ToArray();

    private static ErrorDetailDto ToErrorDetailDto(ErrorDetail d) =>
        new(ToCamelCase(d.Property),
            d.Value,
            d.Errors,
            d.Attributes,
            d.Children is null ? null : d.Children.Select(ToErrorDetailDto).ToArray());

    private static string ToCamelCase(string s) =>
        string.IsNullOrEmpty(s) ? s : char.ToLowerInvariant(s[0]) + s[1..];
}