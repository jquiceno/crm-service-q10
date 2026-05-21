using FluentValidation.TestHelper;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using Infrastructure.Validation.FluentValidation.WeatherForecast;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.CreateWeatherForecast;

public sealed class CreateWeatherForecastInputValidatorTests
{
    private readonly CreateWeatherForecastInputValidator _validator = new();

    [Fact]
    public void Validate_WithValidInput_HasNoErrors()
    {
        var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, 20, "Sunny", null);

        var result = _validator.TestValidate(input);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyDate_HasErrorOnDate()
    {
        var input = new CreateWeatherForecastInputDto(default, 20, "Sunny", null);

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.Date);
    }

    [Theory]
    [InlineData(-91)]
    [InlineData(61)]
    public void Validate_WithTemperatureOutOfRange_HasErrorOnTemperatureC(int temperature)
    {
        var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, temperature, "Sunny", null);

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.Temperature);
    }

    [Fact]
    public void Validate_WithEmptySummary_HasErrorOnSummary()
    {
        var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, 20, "", null);

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.Summary);
    }

    [Fact]
    public void Validate_WithSummaryLongerThan200Chars_HasErrorOnSummary()
    {
        var input = new CreateWeatherForecastInputDto(DateTime.UtcNow, 20, new string('a', 201), null);

        var result = _validator.TestValidate(input);

        result.ShouldHaveValidationErrorFor(x => x.Summary);
    }
}
