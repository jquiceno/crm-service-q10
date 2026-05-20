using Api.Mapping;
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
        var response = context.HttpContext.Response;

        if (result.IsSuccess)
        {
            response.StatusCode = StatusCodes.Status200OK;
            var body = new ApiSuccessResponse<PagedPayload<T>>(
                new PagedPayload<T>(result.Items, result.TotalCount),
                StatusCodes.Status200OK);
            await response.WriteAsJsonAsync(body, JsonSerializerOptions.Web, context.HttpContext.RequestAborted);
            return;
        }

        var error = result.Error;
        var statusCode = (int)ErrorHttpMapper.ToHttpStatusCode(error.Type);
        response.StatusCode = statusCode;

        var errorDto = new ErrorDto(
            ErrorHttpMapper.ToErrorTypeName(error.Type),
            ErrorHttpMapper.ToErrorCode(error.Type),
            error.Message,
            ErrorHttpMapper.ToErrorDetailDtos(error.Details));

        await response.WriteAsJsonAsync(
            new ApiErrorResponse(errorDto, statusCode),
            JsonSerializerOptions.Web,
            context.HttpContext.RequestAborted);
    }
}

internal sealed record PagedPayload<T>(IReadOnlyList<T> Items, int TotalCount);
