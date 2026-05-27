using Microsoft.AspNetCore.Http;
using Shared.Result;

namespace Shared.Presentation.Results;

public sealed class HttpOkResult<T>(Result<T> result) : HttpResult<T>(result)
{
    protected override int SuccessStatusCode => StatusCodes.Status200OK;

    public static implicit operator HttpOkResult<T>(Result<T> result) => new(result);
}
