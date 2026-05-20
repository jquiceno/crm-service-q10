using Shared.Domain.Result;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(IWeatherForecastRepository repository) : ICreateWeatherForecastUseCase
{
    private const string Origin = nameof(CreateWeatherForecastUseCase);

    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        var existsResult = await repository.ExistsForDateAsync(input.Date, cancellationToken);
        if (existsResult.IsFailure)
            return existsResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };
        if (existsResult.Value)
            return WeatherForecastErrors.DateAlreadyExists with
                { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregate = aggregateResult.Value;

        var addResult = await repository.AddAsync(aggregate, cancellationToken);
        if (addResult.IsFailure)
            return addResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        var saveResult = await repository.SaveChangesAsync(cancellationToken);
        if (saveResult.IsFailure)
            return saveResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        return aggregate.ToCreateDto();
    }
}
