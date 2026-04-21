using FluentValidation;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.WeatherForecast;

public sealed class CreateWeatherForecastInputValidator : AbstractValidator<CreateWeatherForecastInputDto>
{
    public CreateWeatherForecastInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithErrorCode(WeatherForecastErrors.DateRequired.Code)
            .WithMessage(WeatherForecastErrors.DateRequired.Message);

        RuleFor(x => x.TemperatureC)
            .InclusiveBetween(WeatherForecastAggregate.MinTemperatureC, WeatherForecastAggregate.MaxTemperatureC)
            .WithErrorCode(WeatherForecastErrors.TemperatureOutOfRange.Code)
            .WithMessage(WeatherForecastErrors.TemperatureOutOfRange.Message);

        RuleFor(x => x.Summary)
            .NotEmpty()
            .WithErrorCode(WeatherForecastErrors.SummaryRequired.Code)
            .WithMessage(WeatherForecastErrors.SummaryRequired.Message);

        RuleFor(x => x.Summary)
            .MaximumLength(WeatherForecastAggregate.MaxSummaryLength)
            .WithErrorCode(WeatherForecastErrors.SummaryTooLong.Code)
            .WithMessage(WeatherForecastErrors.SummaryTooLong.Message);
    }
}
