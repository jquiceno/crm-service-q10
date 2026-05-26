using Api.Mapping;
using Api.Responses;
using Shared.Domain.Errors;
using System.Text.Json;

namespace Api.Results;

internal static class ActionResultHelper
{
    internal static async Task WriteErrorResponseAsync(
        HttpResponse response,
        DomainError error,
        CancellationToken cancellationToken = default)
    {
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
            cancellationToken).ConfigureAwait(false);
    }
}
