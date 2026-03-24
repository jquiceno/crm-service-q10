namespace Application.UseCases.Example;

public sealed record ExampleOutputDto(Guid Id, string Name, string Email, DateTime CreatedAtUtc);
