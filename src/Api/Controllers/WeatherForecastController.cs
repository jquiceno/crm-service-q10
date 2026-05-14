using Api.Attributes;
using Api.Results;
using Api.Filters;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    [OutputCache(Duration = 60, Tags = ["weather-forecasts"])]
    public async Task<HttpOkResult<IReadOnlyList<GetWeatherForecastOutputDto>>> GetAll(
        IGetWeatherForecastUseCase getWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        return await getWeatherForecastUseCase.ExecuteAsync(cancellationToken);
    }

    [HttpPost]
    [ValidateRequest]
    [OutputCacheInvalidate("weather-forecasts")]
    public async Task<HttpCreatedResult<CreateWeatherForecastOutputDto>> Create(
        [FromBody] CreateWeatherForecastInputDto input,
        ICreateWeatherForecastUseCase createWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        return await createWeatherForecastUseCase.ExecuteAsync(input, cancellationToken);
    }
}