using Shared.Domain.Result;
using WeatherForecast.Application.Ports;
using WeatherForecast.Domain.Ports;
using Shared.Application.Ports;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepositoryPort repository, ILoggerPort<GetWeatherForecastUseCase> logger) : IGetWeatherForecastPort
{
    public async Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        logger.Info("Retrieving all weather forecasts");
        var entities = await repository.GetAllAsync(cancellationToken);

        var dtos = entities.Select(e => e.ToGetDto()).ToList();

        return dtos;
    }
}
