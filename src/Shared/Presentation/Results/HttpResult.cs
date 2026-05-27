using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Presentation.Responses;
using Shared.Results;
using System.Text.Json;

namespace Shared.Presentation.Results;

public abstract class HttpResult<T>(Result<T> result) : IActionResult
{
    protected abstract int SuccessStatusCode { get; }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;

        if (result.IsSuccess)
        {
            response.StatusCode = SuccessStatusCode;
            var body = new ApiSuccessResponse<T>(result.Value, SuccessStatusCode);
            await response.WriteAsJsonAsync(body, JsonSerializerOptions.Web, context.HttpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        await ActionResultHelper.WriteErrorResponseAsync(
            response,
            result.Error,
            context.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}
