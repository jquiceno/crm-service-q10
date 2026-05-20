using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Domain.Result;
using WeatherForecast.Application.Ports;
using WeatherForecast.Domain.Ports;

namespace WeatherForecast.Application.UseCases.GetWeatherForecast;

public sealed class GetWeatherForecastUseCase(
    IWeatherForecastRepositoryPort repository, ILoggerPort<GetWeatherForecastUseCase> logger) : IGetWeatherForecastPort
{
    public async Task<PagedResult<GetWeatherForecastOutputDto>> ExecuteAsync(
        PageQuery page,
        CancellationToken cancellationToken = default)
    {
        logger.Info("Retrieving weather forecasts (page {PageIndex}, size {PageSize})", page.PageIndex, page.PageSize);

        var result = await repository.GetAllAsync(page, cancellationToken);
        if (result.IsFailure)
            return result.Error;

        IReadOnlyList<GetWeatherForecastOutputDto> dtos = result.Items
            .Select(e => e.ToGetDto())
            .ToList();

        return PagedResult<GetWeatherForecastOutputDto>.Success(dtos, result.TotalCount);
    }
}
