using Api.Attributes;
using Api.Filters;
using Api.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WeatherForecast.Application.Ports;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    [Tags("weather")]
    [ProducesResponseType(typeof(IReadOnlyList<GetWeatherForecastOutputDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Get all weather forecasts")]
    [EndpointDescription("Retrieves all weather forecasts from the database.")]
    [OutputCache(Duration = 60, Tags = ["weather-forecasts"])]
    public async Task<HttpOkResult<IReadOnlyList<GetWeatherForecastOutputDto>>> GetAll(
        IGetWeatherForecastPort getWeatherForecastPort,
        CancellationToken cancellationToken)
    {
        return await getWeatherForecastPort.ExecuteAsync(cancellationToken);
    }

    [HttpPost]
    [Tags("weather")]
    [ValidateRequest]
    [OutputCacheInvalidate("weather-forecasts")]
    [EndpointSummary("Create a new weather forecast")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointDescription("Creates a new weather forecast in the database.")]
    public async Task<HttpCreatedResult<CreateWeatherForecastOutputDto>> Create(
        [FromBody] CreateWeatherForecastInputDto input,
        ICreateWeatherForecastPort createWeatherForecastPort,
        CancellationToken cancellationToken)
    {
        return await createWeatherForecastPort.ExecuteAsync(input, cancellationToken);
    }
}