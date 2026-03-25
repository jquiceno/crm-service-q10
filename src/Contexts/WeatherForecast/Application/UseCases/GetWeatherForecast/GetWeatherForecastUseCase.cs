using WeatherForecast.Domain.Common;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed class GetWeatherForecastUseCase(
    IRepository<WeatherForecastEntity> repository) : IGetWeatherForecastUseCase
{
    public async Task<Result<IReadOnlyList<GetWeatherForecastOutputDto>>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await repository.GetAllAsync(cancellationToken);

        var dtos = entities.Select(e => e.ToGetDto()).ToList().AsReadOnly();

        return Result<IReadOnlyList<GetWeatherForecastOutputDto>>.Success(dtos);
    }
}
