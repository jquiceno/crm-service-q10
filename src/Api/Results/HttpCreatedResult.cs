using Shared.Domain;

namespace Api.Results;

public sealed class HttpCreatedResult<T>(Result<T> result) : HttpResult<T>(result)
{
    protected override int SuccessStatusCode => StatusCodes.Status201Created;

    public static implicit operator HttpCreatedResult<T>(Result<T> result) => new(result);
}