using Shared.Application.Interfaces;
using Shared.Domain.Result;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepository repository, ILoggerService<GetWeatherForecastUseCase> logger) : IGetWeatherForecastUseCase
{
    public async Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        logger.Info("Retrieving all weather forecasts");

        var forecastsResult = await repository.GetAllAsync(cancellationToken);
        if (forecastsResult.IsFailure)
            return forecastsResult.Error;

        IReadOnlyList<GetWeatherForecastOutputDto> dtos = forecastsResult.Value
            .Select(e => e.ToGetDto())
            .ToList();

        return Result<IReadOnlyList<GetWeatherForecastOutputDto>>.Success(dtos);
    }
}
