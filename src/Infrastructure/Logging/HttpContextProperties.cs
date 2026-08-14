namespace Infrastructure.Logging;

public sealed record HttpContextProperties(
    string UserAgent,
    string RemoteAddress,
    string Method,
    string Route
);
