using FluentValidation;
using FluentValidation.Results;
using NSubstitute;
using Shouldly;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.Interfaces;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCaseTests
{
    private readonly IValidator<CreateWeatherForecastInputDto> _validator =
        Substitute.For<IValidator<CreateWeatherForecastInputDto>>();

    private readonly IWeatherForecastRepository _repository =
        Substitute.For<IWeatherForecastRepository>();

    private readonly CreateWeatherForecastUseCase _sut;

    public CreateWeatherForecastUseCaseTests()
    {
        _sut = new CreateWeatherForecastUseCase(_validator, _repository);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_PersistsEntityAndReturnsSuccess()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny");
        _validator.ValidateAsync(input, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Date.ShouldBe(input.Date);
        result.Value.TemperatureC.ShouldBe(input.TemperatureC);
        result.Value.Summary.ShouldBe(input.Summary);

        await _repository.Received(1).AddAsync(Arg.Any<WeatherForecastEntity>(), Arg.Any<CancellationToken>());
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidInput_ReturnsValidationFailure_AndDoesNotTouchRepository()
    {
        var input = new CreateWeatherForecastInputDto(default, 999, "");
        var failures = new List<ValidationFailure>
        {
            new("Date", "Date is required."),
            new("Summary", "Summary is required.")
        };
        _validator.ValidateAsync(input, Arg.Any<CancellationToken>())
            .Returns(new ValidationResult(failures));

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("Validation");
        result.Error.Description.ShouldContain("Date is required.");
        result.Error.Description.ShouldContain("Summary is required.");

        await _repository.DidNotReceive().AddAsync(Arg.Any<WeatherForecastEntity>(), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
