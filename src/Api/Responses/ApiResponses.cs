using System.Text.Json.Serialization;

namespace Api.Responses;

public sealed record ApiSuccessResponse<T>(T Data, int StatusCode);

public sealed record ApiErrorResponse(ErrorDto Error, int StatusCode);

public sealed record ErrorDto(
    string Message,
    string Type,
    IReadOnlyList<ErrorAttributeDto> Attributes);

public sealed record ErrorAttributeDto(
    string Property,
    IReadOnlyList<string> Messages,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, object?>? Details);
