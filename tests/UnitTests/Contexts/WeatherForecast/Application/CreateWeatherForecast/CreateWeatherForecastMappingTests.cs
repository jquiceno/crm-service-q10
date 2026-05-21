using Shouldly;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.CreateWeatherForecast;

public sealed class CreateWeatherForecastMappingTests
{
    [Fact]
    public void ToAggregate_PreservesInputFields_AndAssignsId()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny", null);

        var result = input.ToAggregate();

        result.IsSuccess.ShouldBeTrue();
        var aggregate = result.Value;
        aggregate.Id.ShouldNotBe(Guid.Empty);
        aggregate.Date.ShouldBe(input.Date);
        aggregate.TemperatureCelsius.ShouldBe(input.Temperature);
        aggregate.Summary.ShouldBe(input.Summary);
    }

    [Fact]
    public void ToCreateDto_ProjectsAggregateFields()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny", null);
        var aggregate = input.ToAggregate().Value;

        var dto = aggregate.ToCreateDto();

        dto.Id.ShouldBe(aggregate.Id);
        dto.Date.ShouldBe(aggregate.Date);
        dto.Temperature.ShouldBe(aggregate.TemperatureCelsius);
        dto.TemperatureFahrenheit.ShouldBe(aggregate.TemperatureFahrenheit);
        dto.Summary.ShouldBe(aggregate.Summary);
        dto.CreatedAtUtc.ShouldBe(aggregate.CreatedAtUtc);
    }
}
