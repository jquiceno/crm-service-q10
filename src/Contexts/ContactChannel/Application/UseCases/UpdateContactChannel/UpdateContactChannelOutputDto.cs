using System.ComponentModel;

namespace ContactChannel.Application.UseCases.UpdateContactChannel;

public sealed record UpdateContactChannelOutputDto(
    [property: Description("Identifier of the updated contact channel.")]
    int Id);
