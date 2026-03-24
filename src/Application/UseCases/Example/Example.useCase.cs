using Domain.Common;
using FluentValidation;

namespace Application.UseCases.Example;

public sealed class ExampleUseCase(
    IValidator<ExampleInputDto> validator)
{
    public async Task<Result<ExampleOutputDto>> ExecuteAsync(
        ExampleInputDto input,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);

        if (!validationResult.IsValid)
        {
            var errors = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result<ExampleOutputDto>.Failure(new Error("Validation.Failed", errors));
        }

        // TODO: Replace with actual domain logic and repository calls
        // var entity = input.ToEntity();
        // await repository.AddAsync(entity, cancellationToken);
        // await repository.SaveChangesAsync(cancellationToken);
        // return Result.Success(entity.ToDto());

        var output = new ExampleOutputDto(
            Id: Guid.NewGuid(),
            Name: input.Name,
            Email: input.Email,
            CreatedAtUtc: DateTime.UtcNow);

        return Result<ExampleOutputDto>.Success(output);
    }
}
