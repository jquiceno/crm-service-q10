using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Presentation.Responses;
using Shared.Results;
using System.Text.Json;

namespace Shared.Presentation.Results;

public sealed class HttpOkPagedResult<T>(PagedResult<T> result) : IActionResult
{
    public static implicit operator HttpOkPagedResult<T>(PagedResult<T> result) => new(result);

    public async Task ExecuteResultAsync(ActionContext context)
    {
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

/// <summary>
/// Body of a successful paged response: <c>{ "items": [...], "totalCount": n }</c>. Public because
/// controllers name it in <c>[ProducesResponseType(typeof(ApiSuccessResponse&lt;PagedPayload&lt;T&gt;&gt;), 200)]</c>
/// to publish the real schema of a paged endpoint, as controllers.md §5.5 prescribes.
/// </summary>
public sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount);
