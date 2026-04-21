using Shared.Application.Interfaces;
using Shared.Domain;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(
    IInputValidator<CreateWeatherForecastInputDto> validator,
    IWeatherForecastRepository repository) : ICreateWeatherForecastUseCase
{
    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        var validationResult = await validator.ValidateAsync(input, cancellationToken);
        if (validationResult.IsFailure)
            return validationResult.Error;

        var aggregate = input.ToAggregate();

        await repository.AddAsync(aggregate, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return aggregate.ToCreateDto();
    }
}
