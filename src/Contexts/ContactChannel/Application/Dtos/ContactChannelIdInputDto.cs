using System.ComponentModel;

namespace ContactChannel.Application.Dtos;

public sealed record ContactChannelIdInputDto(
    [property: Description("Identifier of the contact channel. Must be greater than zero.")]
    int Id);
