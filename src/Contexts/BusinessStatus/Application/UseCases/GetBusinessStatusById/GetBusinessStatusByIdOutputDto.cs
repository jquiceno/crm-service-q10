using System.ComponentModel;

namespace BusinessStatus.Application.UseCases.GetBusinessStatusById;

public sealed record GetBusinessStatusByIdOutputDto(
    [property: Description("Identifier of the business status.")]
    int Id,
    [property: Description("Name of the stage.")]
    string Name,
    [property: Description("Progress percentage as a whole number. 0 is 'Perdido' and 100 is 'Ganado'; null when the stored row has no percentage.")]
    int? Percentage,
    [property: Description("Six hexadecimal characters without '#', or null when the stage has no colour.")]
    string? Color,
    [property: Description("Whether the stage is active.")]
    bool IsActive);
