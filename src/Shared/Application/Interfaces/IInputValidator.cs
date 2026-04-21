using Shared.Domain;

namespace Shared.Application.Interfaces;

public interface IInputValidator<T>
{
    Task<Result> ValidateAsync(T input, CancellationToken cancellationToken = default);
}
