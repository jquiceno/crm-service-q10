using Shared.Domain.Result;
using WeatherForecast.Application.Ports;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Ports;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(
    IWeatherForecastRepositoryPort repository) : ICreateWeatherForecastPort
{
    private const string Origin = nameof(CreateWeatherForecastUseCase);

    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsForDateAsync(input.Date, cancellationToken))
            return WeatherForecastErrors.DateAlreadyExists with
                { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregate = aggregateResult.Value;
        await repository.AddAsync(aggregate, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return aggregate.ToCreateDto();
    }
}
