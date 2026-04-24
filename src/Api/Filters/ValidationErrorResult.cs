using Api.Mapping;
using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain;
using System.Text.Json;

namespace Api.Filters;

internal sealed class ValidationErrorResult(Error error) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
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

        await response.WriteAsJsonAsync(
            new ApiErrorResponse(errorDto, statusCode),
            JsonSerializerOptions.Web,
            context.HttpContext.RequestAborted);
    }
}