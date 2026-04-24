using Api.Mapping;
using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain;
using System.Text.Json;

namespace Api.Results;

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
            await response.WriteAsJsonAsync(body, JsonSerializerOptions.Web, context.HttpContext.RequestAborted);
            return;
        }

        var error = result.Error;
        var statusCode = (int)ErrorHttpMapper.ToHttpStatusCode(error.Type);
        response.StatusCode = statusCode;

        var details = error.Details
            .Select(d => new ErrorDetailDto(d.Code, d.Message, d.Type.ToString().ToLowerInvariant(), d.Context))
            .ToArray();

        var errorDto = new ErrorDto(
            error.Code,
            error.Message,
            error.Type.ToString().ToLowerInvariant(),
            details, error.Context);

        var bodyError = new ApiErrorResponse(errorDto, statusCode);
        await response.WriteAsJsonAsync(bodyError, JsonSerializerOptions.Web, context.HttpContext.RequestAborted);
    }
}