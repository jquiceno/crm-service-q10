using System.ComponentModel;

namespace BusinessStatus.Application.UseCases.CreateBusinessStatus;

/// <summary>
/// Response body of <c>POST /business-statuses</c>: the created resource, including the identifier
/// the database assigned.
/// </summary>
public sealed record CreateBusinessStatusOutputDto(
    [property: Description("Identifier assigned to the business status.")]
    int Id,
    [property: Description("Business status name.")]
    string Name,
    [property: Description("Progress percentage of the business status.")]
    int? Percentage,
    [property: Description("Stage colour as 6 hexadecimal characters without '#', or null when the business status has no colour.")]
    string? Color,
    [property: Description("Whether the business status is active.")]
    bool IsActive);
