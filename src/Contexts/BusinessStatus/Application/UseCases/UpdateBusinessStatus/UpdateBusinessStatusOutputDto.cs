using System.ComponentModel;

namespace BusinessStatus.Application.UseCases.UpdateBusinessStatus;

/// <summary>
/// Response body of <c>PUT /business-statuses/{id}</c>: the resource as it stands after the
/// replacement.
/// </summary>
public sealed record UpdateBusinessStatusOutputDto(
    [property: Description("Identifier of the business status.")]
    int Id,
    [property: Description("Business status name.")]
    string Name,
    [property: Description("Progress percentage of the business status.")]
    int? Percentage,
    [property: Description("Stage colour as 6 hexadecimal characters without '#', or null when the business status has no colour.")]
    string? Color,
    [property: Description("Whether the business status is active.")]
    bool IsActive);
