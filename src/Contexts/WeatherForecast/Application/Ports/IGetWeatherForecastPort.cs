using Shared.Domain.Result;
using WeatherForecast.Application.UseCases.GetWeatherForecast;

namespace WeatherForecast.Application.Ports;

public interface IGetWeatherForecastPort
{
    Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default);
}
