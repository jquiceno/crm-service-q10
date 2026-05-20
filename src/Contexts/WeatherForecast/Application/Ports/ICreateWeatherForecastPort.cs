using Shared.Domain.Result;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;

namespace WeatherForecast.Application.Ports;

public interface ICreateWeatherForecastPort
{
    Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default);
}
