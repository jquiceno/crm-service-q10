using WeatherForecast.Domain.Common;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public interface ICreateWeatherForecastUseCase
{
    Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default);
}
