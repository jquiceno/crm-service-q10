using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Errors;
using Shared.Domain.Result;
using Shouldly;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Ports;
using WeatherForecast.Domain.ValueObjects;
using Xunit;

namespace UnitTests.Contexts.WeatherForecast.Application.CreateWeatherForecast;

public sealed class CreateWeatherForecastUseCaseTests
{
    private readonly IWeatherForecastRepositoryPort _repository =
        Substitute.For<IWeatherForecastRepositoryPort>();

    private readonly IUnitOfWorkPort _unitOfWork =
        Substitute.For<IUnitOfWorkPort>();

    private readonly CreateWeatherForecastUseCase _sut;

    public CreateWeatherForecastUseCaseTests()
    {
        _sut = new CreateWeatherForecastUseCase(_repository, _unitOfWork);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_PersistsAggregateAndReturnsSuccess()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny", null);
        _repository.ExistsForDateAsync(input.Date, Arg.Any<CancellationToken>()).Returns(false);
        _repository.AddAsync(Arg.Any<WeatherForecastAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Date.ShouldBe(input.Date);
        result.Value.Temperature.ShouldBe(input.Temperature);
        result.Value.Summary.ShouldBe(input.Summary);

        await _repository.Received(1).ExistsForDateAsync(input.Date, Arg.Any<CancellationToken>());
        await _repository.Received(1).AddAsync(Arg.Any<WeatherForecastAggregate>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDateAlreadyExists_ReturnsConflict_AndDoesNotPersist()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 25, "Sunny", null);
        _repository.ExistsForDateAsync(input.Date, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Message.ShouldBe(WeatherForecastErrors.DateAlreadyExists.Message);
        result.Error.Context.ShouldBe(WeatherForecastErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateWeatherForecastUseCase));

        await _repository.DidNotReceive().AddAsync(Arg.Any<WeatherForecastAggregate>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidDomainInput_ReturnsDomainValidationFailure_AndDoesNotPersist()
    {
        var input = new CreateWeatherForecastInputDto(new DateTime(2026, 4, 21), 999, "Sunny", null);
        _repository.ExistsForDateAsync(input.Date, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _sut.ExecuteAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Message.ShouldBe("Domain validation failed.");
        result.Error.Context.ShouldBe(WeatherForecastErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateWeatherForecastUseCase));
        result.Error.Details.ShouldContain(d =>
            d.Property == nameof(Temperature) &&
            d.Errors!.Contains(WeatherForecastErrors.TemperatureOutOfRange.Message));

        await _repository.DidNotReceive().AddAsync(Arg.Any<WeatherForecastAggregate>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
