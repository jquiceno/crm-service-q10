using Api.Filters;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/weather-forecasts")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    [HttpCache(ttlSeconds: 60)]
    public async Task<IActionResult> GetAll(
        IGetWeatherForecastUseCase getWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        var result = await getWeatherForecastUseCase.ExecuteAsync(cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(result.Value);
    }

    [HttpPost]
    [InvalidateCache("api/v1/weather-forecasts")]
    public async Task<IActionResult> Create(
        [FromBody] CreateWeatherForecastInputDto input,
        ICreateWeatherForecastUseCase createWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        var result = await createWeatherForecastUseCase.ExecuteAsync(input, cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Created(string.Empty, result.Value);
    }
}
