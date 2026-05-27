using Microsoft.AspNetCore.Http;
using Shared.Result;

namespace Shared.Presentation.Results;

public sealed class HttpCreatedResult<T>(Result<T> result) : HttpResult<T>(result)
{
    protected override int SuccessStatusCode => StatusCodes.Status201Created;

    public static implicit operator HttpCreatedResult<T>(Result<T> result) => new(result);
}
