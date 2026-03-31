using WeatherForecast.Domain.Common;
using WeatherForecast.Domain.Interfaces;
using Shared.Application.Interfaces;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepository repository, ILoggerService<GetWeatherForecastUseCase> logger) : IGetWeatherForecastUseCase
{
    public async Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Retrieving all weather forecasts");
        var entities = await repository.GetAllAsync(cancellationToken);

        var dtos = entities.Select(e => e.ToGetDto()).ToList().AsReadOnly();

        return Result<IReadOnlyList<GetWeatherForecastOutputDto>>.Success(dtos);
    }
}
