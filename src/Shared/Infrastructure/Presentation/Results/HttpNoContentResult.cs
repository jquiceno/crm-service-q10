using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Results;

namespace Shared.Presentation.Results;

public sealed class HttpNoContentResult(Result result) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        if (result.IsSuccess)
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status204NoContent;
            return;
        }

        await ActionResultHelper.WriteErrorResponseAsync(
            context.HttpContext.Response,
            result.Error,
            context.HttpContext.RequestAborted).ConfigureAwait(false);
    }

    public static implicit operator HttpNoContentResult(Result result) => new(result);
}
