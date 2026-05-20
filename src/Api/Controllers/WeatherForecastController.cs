using Api.Attributes;
using Api.Filters;
using Api.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shared.Application.Dtos;
using Shared.Domain.Pagination;
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
    [ValidateRequest]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [EndpointSummary("Get all weather forecasts")]
    [EndpointDescription("Retrieves a paginated list of weather forecasts.")]
    [OutputCache(Duration = 60, Tags = ["weather-forecasts"])]
    public async Task<HttpOkPagedResult<GetWeatherForecastOutputDto>> GetAll(
        [FromQuery] PageQueryInputDto pagination,
        IGetWeatherForecastPort? getWeatherForecastPort = null,
        CancellationToken cancellationToken = default)
    {
        return await getWeatherForecastPort!.ExecuteAsync(
            new PageQuery(pagination.PageIndex, pagination.PageSize),
            cancellationToken);
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