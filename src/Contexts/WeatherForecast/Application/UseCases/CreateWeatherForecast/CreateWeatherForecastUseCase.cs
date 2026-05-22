using Shared.Application.Ports;
using Shared.Domain.Result;
using WeatherForecast.Application.Ports;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Ports;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCase(
    IWeatherForecastRepositoryPort repository,
    IUnitOfWorkPort unitOfWork) : ICreateWeatherForecastPort
{
    private const string Origin = nameof(CreateWeatherForecastUseCase);

    public async Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var existsResult = await repository.ExistsForDateAsync(input.Date, cancellationToken).ConfigureAwait(false);
        if (existsResult.IsFailure)
            return existsResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };
        if (existsResult.Value)
            return WeatherForecastErrors.DateAlreadyExists with
            { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregateResult = input.ToAggregate();
        if (aggregateResult.IsFailure)
            return aggregateResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        var aggregate = aggregateResult.Value;

        var addResult = await repository.AddAsync(aggregate, cancellationToken).ConfigureAwait(false);
        if (addResult.IsFailure)
            return addResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        var commitResult = await unitOfWork.CommitAsync(cancellationToken).ConfigureAwait(false);
        if (commitResult.IsFailure)
            return commitResult.Error with { Context = WeatherForecastErrors.Context, Origin = Origin };

        return aggregate.ToCreateDto();
    }
}
