namespace Shared.Application.Dtos;

// Wraps an identifier taken from the route so it reaches the structural validation layer.
// ValidateRequestFilter skips simple types, so a bare int parameter is never validated no matter
// what validator exists for it; a record is what makes SequenceIdInputValidator run.
public sealed record SequenceIdInputDto(int Id);
