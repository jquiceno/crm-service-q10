using FluentValidation;
using Shared.Application;
using Shared.Domain;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(
    IValidator<CreateWeatherForecastInputDto> validator,
    IWeatherForecastRepository repository) : ICreateWeatherForecastUseCase
{
    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);

        if (!validationResult.IsValid)
        {
            var details = validationResult.Errors
                .Select(e => new Error(e.ErrorCode, e.ErrorMessage, ErrorType.Validation))
                .ToList<Error>();

            return ApplicationErrors.ValidationFailed(details);
        }

        var aggregate = input.ToAggregate();

        await repository.AddAsync(aggregate, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return aggregate.ToCreateDto();
    }
}
