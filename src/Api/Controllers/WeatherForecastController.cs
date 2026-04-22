using Api.Attributes;
using Api.Results;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class WeatherForecastController() : ControllerBase
{
    [HttpGet]
    public async Task<HttpOkResult<IReadOnlyList<GetWeatherForecastOutputDto>>> GetAll(
        IGetWeatherForecastUseCase getWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        return await getWeatherForecastUseCase.ExecuteAsync(cancellationToken);
    }

    [HttpPost]
    [ValidateRequest]
    public async Task<HttpCreatedResult<CreateWeatherForecastOutputDto>> Create(
        [FromBody] CreateWeatherForecastInputDto input,
        ICreateWeatherForecastUseCase createWeatherForecastUseCase,
        CancellationToken cancellationToken)
    {
        return await createWeatherForecastUseCase.ExecuteAsync(input, cancellationToken);
    }
}