using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain.Result;
using System.Text.Json;

namespace Api.Results;

public abstract class HttpResult<T>(Result<T> result) : IActionResult
{
    protected abstract int SuccessStatusCode { get; }

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

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
