namespace Infrastructure.Logging;

public sealed record HttpRequestLogProperties(
    string UserAgent,
    string RemoteAddress,
    string Method,
    string Route,
    int StatusCode,
    long LatencyMs
);
