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
/// Body of a paged success response. Public because the controllers name it in their
/// <c>[ProducesResponseType]</c>, which is what publishes the shape to OpenAPI.
/// </summary>
public sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount);
