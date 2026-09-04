using System.ComponentModel;

namespace Shared.Application.Dtos;

public sealed record ConsecutiveIdInputDto(
    [property: Description("Consecutive identifier of the resource. Must be greater than zero.")]
    int Id);
