using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Presentation.Mapping;
using Shared.Presentation.Responses;
using Shared.Results.Errors;
using System.Text.Json;

namespace Shared.Presentation.Filters;

public sealed class ValidationErrorResult(DomainError error) : IActionResult
{
    public async Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.HttpContext.Response;
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
            context.HttpContext.RequestAborted).ConfigureAwait(false);
    }
}
