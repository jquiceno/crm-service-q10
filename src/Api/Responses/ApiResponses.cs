using System.Text.Json.Serialization;

namespace Api.Responses;

public sealed record ApiSuccessResponse<T>(T Data, int StatusCode);

public sealed record ApiErrorResponse(ErrorDto Error, int StatusCode);

public sealed record ErrorDto(
    string Type,
    string Message,
    IReadOnlyList<ErrorDetailDto> Details);

public sealed record ErrorDetailDto(
    string Property,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    object? Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<string>? Errors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, object?>? Attributes = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyList<ErrorDetailDto>? Children = null);
