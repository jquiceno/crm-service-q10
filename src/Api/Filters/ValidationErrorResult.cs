using Api.Mapping;
using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain;
using System.Text.Json;

namespace Api.Filters;

internal sealed class ValidationErrorResult(DomainError error) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        var response = context.HttpContext.Response;
        var statusCode = (int)ErrorHttpMapper.ToHttpStatusCode(error.Type);
        response.StatusCode = statusCode;

        var errorDto = new ErrorDto(
            error.Message,
            error.Type.ToString().ToLowerInvariant(),
            ErrorHttpMapper.ToErrorAttributeDtos(error.Attributes));

        await response.WriteAsJsonAsync(
            new ApiErrorResponse(errorDto, statusCode),
            JsonSerializerOptions.Web,
            context.HttpContext.RequestAborted);
    }
}
