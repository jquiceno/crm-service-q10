using System.ComponentModel;

namespace Shared.Application.Dtos;

public sealed record ResourceIdInputDto(
    [property: Description("Identifier of the resource. Must be greater than zero.")]
    int Id);
