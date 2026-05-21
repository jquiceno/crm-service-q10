using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Domain.Result;
using Shouldly;
using UnitTests.TestSupport.Builders;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Ports;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.GetWeatherForecast;

public sealed class GetWeatherForecastUseCaseTests
{
    private readonly IWeatherForecastRepositoryPort _repository =
        Substitute.For<IWeatherForecastRepositoryPort>();

    private readonly ILoggerPort<GetWeatherForecastUseCase> _logger =
        Substitute.For<ILoggerPort<GetWeatherForecastUseCase>>();

    private readonly GetWeatherForecastUseCase _sut;

    public GetWeatherForecastUseCaseTests()
    {
        _sut = new GetWeatherForecastUseCase(_repository, _logger);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsAggregates_ReturnsMappedDtos()
    {
        var page = new PageQuery(0, 20);
        var aggregates = new List<WeatherForecastAggregate>
        {
            WeatherForecastAggregate.FromEntity(
                new WeatherForecastEntityBuilder().WithSummary("Sunny").WithTemperatureC(25).Build()),
            WeatherForecastAggregate.FromEntity(
                new WeatherForecastEntityBuilder().WithSummary("Cold").WithTemperatureC(-5).Build()),
        };
        _repository.GetAllAsync(page, Arg.Any<CancellationToken>())
            .Returns(PagedResult<WeatherForecastAggregate>.Success(aggregates, aggregates.Count));

        var result = await _sut.ExecuteAsync(page, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Items.Count.ShouldBe(2);
        result.TotalCount.ShouldBe(2);
        result.Items[0].Summary.ShouldBe("Sunny");
        result.Items[0].Temperature.ShouldBe(25);
        result.Items[1].Summary.ShouldBe("Cold");
        result.Items[1].Temperature.ShouldBe(-5);

        _logger.Received(1).Info(
            "Retrieving weather forecasts (page {PageIndex}, size {PageSize})",
            page.PageIndex,
            page.PageSize);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsEmpty_ReturnsEmptySuccess()
    {
        var page = new PageQuery(0, 20);
        _repository.GetAllAsync(page, Arg.Any<CancellationToken>())
            .Returns(PagedResult<WeatherForecastAggregate>.Success([], 0));

        var result = await _sut.ExecuteAsync(page, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }
}
