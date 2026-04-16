using Api.Mapping;
using Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Shared.Domain;

namespace Api.Extensions;

public static class ResultExtensions
{
    public static IActionResult ToApiResponse<T>(this Result<T> result, ControllerBase controller)
    {
        return result.Match(
            value =>
            {
                var statusCode = (int)System.Net.HttpStatusCode.OK;
                return controller.StatusCode(statusCode, new ApiSuccessResponse<T>(value, statusCode));
            },
            error => ToErrorResponse(error, controller));
    }

    public static IActionResult ToCreatedApiResponse<T>(this Result<T> result, ControllerBase controller)
    {
        return result.Match(
            value =>
            {
                var statusCode = (int)System.Net.HttpStatusCode.Created;
                return controller.StatusCode(statusCode, new ApiSuccessResponse<T>(value, statusCode));
            },
            error => ToErrorResponse(error, controller));
    }

    private static IActionResult ToErrorResponse(Error error, ControllerBase controller)
    {
        var httpStatus = ErrorHttpMapper.ToHttpStatusCode(error.Type);
        var statusCode = (int)httpStatus;

        var details = error.Details
            .Select(d => new ErrorDetailDto(d.Code, d.Message, d.Type.ToString().ToLowerInvariant()))
            .ToArray();

        var errorDto = new ErrorDto(
            error.Code,
            error.Message,
            error.Type.ToString().ToLowerInvariant(),
            details);

        return controller.StatusCode(statusCode, new ApiErrorResponse(errorDto, statusCode));
    }
}
