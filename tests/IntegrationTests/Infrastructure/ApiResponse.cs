namespace IntegrationTests.Infrastructure;

/// <summary>
/// Mirrors the response envelope shape returned by <c>HttpOkResult</c>.
/// </summary>
public sealed record ApiResponse<T>(T Data, int StatusCode);
