using FluentValidation;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Errors;

namespace Infrastructure.Validation.FluentValidation.WeatherForecast;

public class CreateWeatherForecastInputValidator : AbstractValidator<CreateWeatherForecastInputDto>, IStructuralValidator<CreateWeatherForecastInputDto>
{
    public CreateWeatherForecastInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithErrorCode(WeatherForecastErrors.DateRequired.Code)
            .WithMessage(WeatherForecastErrors.DateRequired.Message);

        RuleFor(x => x.Summary)
            .NotEmpty()
            .WithErrorCode(WeatherForecastErrors.SummaryRequired.Code)
            .WithMessage(WeatherForecastErrors.SummaryRequired.Message);

        RuleFor(x => x.Summary)
            .MaximumLength(WeatherForecastAggregate.MaxSummaryLength)
            .WithErrorCode(WeatherForecastErrors.SummaryTooLong.Code)
            .WithMessage(WeatherForecastErrors.SummaryTooLong.Message)
            .WithState(_ => WeatherForecastErrors.SummaryTooLong.Context);
    }
}