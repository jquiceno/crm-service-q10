using Api.Extensions;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/weather-forecasts")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        IGetWeatherForecastUseCase getWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        var result = await getWeatherForecastUseCase.ExecuteAsync(cancellationToken);
        return result.ToApiResponse(this);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateWeatherForecastInputDto input,
        ICreateWeatherForecastUseCase createWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        var result = await createWeatherForecastUseCase.ExecuteAsync(input, cancellationToken);
        return result.ToCreatedApiResponse(this);
    }
}
