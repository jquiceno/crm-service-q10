using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/weather-forecasts")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<GetWeatherForecastOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Get all weather forecasts")]
    [EndpointDescription("Retrieves all weather forecasts from the database.")]
    [Tags("weather")]
    public async Task<ActionResult<IReadOnlyList<GetWeatherForecastOutputDto>>> GetAll(
        IGetWeatherForecastUseCase getWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        var result = await getWeatherForecastUseCase.ExecuteAsync(cancellationToken);

        if (result.IsFailure)
            return BadRequest(new { error = result.Error.Description });

        return Ok(result.Value);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Create a new weather forecast")]
    [EndpointDescription("Creates a new weather forecast in the database.")]
    [Tags("weather")]
    public async Task<ActionResult<CreateWeatherForecastOutputDto>> Create(
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
