using System.Text.Json.Serialization;

namespace Api.Responses;

public sealed record ApiSuccessResponse<T>(T Data, int StatusCode);

public sealed record ApiErrorResponse(ErrorDto Error, int StatusCode);

public sealed record ErrorDto(
    string Message,
    string Type,
    IReadOnlyList<ErrorDetailDto> Details);

public sealed record ErrorDetailDto(
    string Property,
    IReadOnlyList<string> Messages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, object?>? Attributes);
