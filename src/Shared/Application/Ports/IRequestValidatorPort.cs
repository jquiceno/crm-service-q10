using Shared.Domain.Result;

namespace Shared.Application.Ports;

/// <summary>
/// Non-generic base used by the HTTP filter for dynamic dispatch without knowing T at compile time.
/// </summary>
public interface IRequestValidatorPort
{
    Task<Result> ValidateAsync(object input, CancellationToken cancellationToken = default);
}

/// <summary>
/// Structural validator for HTTP-level validation (format, ranges, required fields).
/// Resolved by <see cref="ValidateRequestAttribute"/> before the action executes.
/// </summary>
public interface IRequestValidatorPort<T> : IRequestValidatorPort
{
    Task<Result> ValidateAsync(T input, CancellationToken cancellationToken = default);
}
