using Shared.Domain.Pagination;
using Shared.Domain.Result;
using WeatherForecast.Application.UseCases.GetWeatherForecast;

namespace WeatherForecast.Application.Ports;

public interface IGetWeatherForecastPort
{
    Task<PagedResult<GetWeatherForecastOutputDto>> ExecuteAsync(
        PageQuery page,
        CancellationToken cancellationToken = default);
}
