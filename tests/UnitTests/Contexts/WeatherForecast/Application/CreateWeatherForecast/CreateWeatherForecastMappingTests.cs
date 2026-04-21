using Shouldly;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.CreateWeatherForecast;

public sealed class CreateWeatherForecastMappingTests
{
    [Fact]
    public void ToEntity_PreservesInputFields_AndAssignsId()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny");

        var entity = input.ToEntity();

        entity.Id.ShouldNotBe(Guid.Empty);
        entity.Date.ShouldBe(input.Date);
        entity.TemperatureC.ShouldBe(input.TemperatureC);
        entity.Summary.ShouldBe(input.Summary);
    }

    [Fact]
    public void ToCreateDto_ProjectsEntityFields()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny");
        var entity = input.ToEntity();

        var dto = entity.ToCreateDto();

        dto.Id.ShouldBe(entity.Id);
        dto.Date.ShouldBe(entity.Date);
        dto.TemperatureC.ShouldBe(entity.TemperatureC);
        dto.TemperatureF.ShouldBe(entity.TemperatureF);
        dto.Summary.ShouldBe(entity.Summary);
        dto.CreatedAtUtc.ShouldBe(entity.CreatedAtUtc);
    }
}
