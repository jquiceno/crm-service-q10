using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using UnitTests.TestSupport.Builders;
using WeatherForecast.Application.UseCases.GetWeatherForecast;
using WeatherForecast.Domain.Entities;
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
    public async Task ExecuteAsync_WhenRepositoryReturnsEntities_ReturnsMappedDtos()
    {
        var entities = new List<WeatherForecastEntity>
        {
            new WeatherForecastEntityBuilder().WithSummary("Sunny").WithTemperatureC(25).Build(),
            new WeatherForecastEntityBuilder().WithSummary("Cold").WithTemperatureC(-5).Build(),
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(entities);

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Count.ShouldBe(2);
        result.Value[0].Summary.ShouldBe("Sunny");
        result.Value[0].TemperatureC.ShouldBe(25);
        result.Value[1].Summary.ShouldBe("Cold");
        result.Value[1].TemperatureC.ShouldBe(-5);

        _logger.Received(1).Info("Retrieving all weather forecasts");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsEmpty_ReturnsEmptySuccess()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<WeatherForecastEntity>());

        var result = await _sut.ExecuteAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }
}
