using Shared.Domain;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public interface IGetWeatherForecastUseCase
{
    Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default);
}
