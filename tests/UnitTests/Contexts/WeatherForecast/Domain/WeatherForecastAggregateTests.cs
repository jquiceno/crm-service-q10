using Shared.Domain.Errors;
using Shouldly;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.ValueObjects;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Domain;

public sealed class WeatherForecastAggregateTests
{
    [Fact]
    public void Create_WithEmptyGuid_ThrowsArgumentException()
    {
        var act = () => WeatherForecastAggregate.Create(Guid.Empty, DateTime.UtcNow, 20, "Sunny");

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Create_WithValidArguments_SetsProperties()
    {
        var id = Guid.NewGuid();
        var date = new DateTime(2026, 4, 21);

        var result = WeatherForecastAggregate.Create(id, date, 25, "Sunny");

        result.IsSuccess.ShouldBeTrue();
        var aggregate = result.Value;
        aggregate.Id.ShouldBe(id);
        aggregate.Date.ShouldBe(date);
        aggregate.TemperatureCelsius.ShouldBe(25);
        aggregate.Summary.ShouldBe("Sunny");
        aggregate.CreatedAtUtc.ShouldBeInRange(
            DateTime.UtcNow.AddSeconds(-5),
            DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public void Create_WithTemperatureOutOfRange_ReturnsDomainValidationFailure()
    {
        var result = WeatherForecastAggregate.Create(Guid.NewGuid(), DateTime.UtcNow, 999, "Sunny");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.ShouldContain(d =>
            d.Property == nameof(Temperature) &&
            d.Errors!.Contains(WeatherForecastErrors.TemperatureOutOfRange.Message));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(25, 77)]
    public void Create_ConvertsTemperatureToFahrenheit(int celsius, int expectedFahrenheit)
    {
        var result = WeatherForecastAggregate.Create(Guid.NewGuid(), DateTime.UtcNow, celsius, "Sunny");

        result.IsSuccess.ShouldBeTrue();
        result.Value.TemperatureFahrenheit.ShouldBe(expectedFahrenheit);
    }
}
