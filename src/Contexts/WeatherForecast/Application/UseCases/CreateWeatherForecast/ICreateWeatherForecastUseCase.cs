using Shared.Domain.Result;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public interface ICreateWeatherForecastUseCase
{
    Task<Result<CreateWeatherForecastOutputDto>> ExecuteAsync(
        CreateWeatherForecastInputDto input, CancellationToken cancellationToken = default);
}
