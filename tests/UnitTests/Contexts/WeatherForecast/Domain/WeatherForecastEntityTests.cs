using Shouldly;
using UnitTests.TestSupport.Builders;
using WeatherForecast.Domain.Entities;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Domain;

public sealed class WeatherForecastEntityTests
{
    [Fact]
    public void Constructor_WithEmptyGuid_ThrowsArgumentException()
    {
        var act = () => new WeatherForecastEntity(Guid.Empty, DateTime.UtcNow, 20, "Sunny");

        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithNullSummary_ThrowsArgumentNullException()
    {
        var act = () => new WeatherForecastEntity(Guid.NewGuid(), DateTime.UtcNow, 20, null!);

        act.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidArguments_SetsProperties()
    {
        var id = Guid.NewGuid();
        var date = new DateTime(2026, 4, 21);

        var entity = new WeatherForecastEntity(id, date, 25, "Sunny");

        entity.Id.ShouldBe(id);
        entity.Date.ShouldBe(date);
        entity.TemperatureC.ShouldBe(25);
        entity.Summary.ShouldBe("Sunny");
        entity.CreatedAtUtc.ShouldBeInRange(
            DateTime.UtcNow.AddSeconds(-5),
            DateTime.UtcNow.AddSeconds(5));
    }

    [Theory]
    [InlineData(0, 32)]
    [InlineData(25, 76)]
    public void TemperatureF_IsConvertedFromCelsius(int celsius, int expectedFahrenheit)
    {
        var entity = new WeatherForecastEntityBuilder()
            .WithTemperatureC(celsius)
            .Build();

        entity.TemperatureF.ShouldBe(expectedFahrenheit);
    }
}
