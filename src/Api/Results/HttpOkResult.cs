using Shared.Domain.Result;

namespace Api.Results;

public sealed class HttpOkResult<T>(Result<T> result) : HttpResult<T>(result)
{
    protected override int SuccessStatusCode => StatusCodes.Status200OK;

    public static implicit operator HttpOkResult<T>(Result<T> result) => new(result);
}