using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain.Result;
using System.Text.Json;

namespace Api.Results;

public sealed class HttpOkPagedResult<T>(PagedResult<T> result) : IActionResult
{
    public static implicit operator HttpOkPagedResult<T>(PagedResult<T> result) => new(result);

    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;

        if (result.IsSuccess)
        {
            response.StatusCode = StatusCodes.Status200OK;
            var body = new ApiSuccessResponse<PagedPayload<T>>(
                new PagedPayload<T>(result.Items, result.TotalCount),
                StatusCodes.Status200OK);
            await response.WriteAsJsonAsync(body, JsonSerializerOptions.Web, context.HttpContext.RequestAborted).ConfigureAwait(false);
            return;
        }

        await ActionResultHelper.WriteErrorResponseAsync(
            response,
            result.Error,
            context.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}

internal sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount);
