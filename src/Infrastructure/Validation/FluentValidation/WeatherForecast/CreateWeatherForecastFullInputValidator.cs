using FluentValidation;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.Interfaces;

namespace Infrastructure.Validation.FluentValidation.WeatherForecast;

public sealed class CreateWeatherForecastFullInputValidator : CreateWeatherForecastInputValidator
{
    public CreateWeatherForecastFullInputValidator(IWeatherForecastRepository repository)
    {
        RuleFor(x => x.Date)
            .MustAsync(async (date, ct) => !await repository.ExistsForDateAsync(date, ct))
            .WithErrorCode(WeatherForecastErrors.DateAlreadyExists.Code)
            .WithMessage(WeatherForecastErrors.DateAlreadyExists.Message);
    }
}