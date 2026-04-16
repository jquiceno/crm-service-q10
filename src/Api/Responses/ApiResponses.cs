namespace Api.Responses;

public sealed record ApiSuccessResponse<T>(T Data, int StatusCode);

public sealed record ApiErrorResponse(ErrorDto Error, int StatusCode);

public sealed record ErrorDto(string Code, string Message, string Type, IReadOnlyList<ErrorDetailDto> Details);

public sealed record ErrorDetailDto(string Code, string Message, string Type);
