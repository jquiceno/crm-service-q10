using FluentValidation;

namespace WeatherForecast.Application.UseCases.CreateWeatherForecast;

public sealed class CreateWeatherForecastInputValidator : AbstractValidator<CreateWeatherForecastInputDto>
{
    public CreateWeatherForecastInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty().WithMessage("Date is required.");

        RuleFor(x => x.TemperatureC)
            .InclusiveBetween(-90, 60).WithMessage("TemperatureC must be between -90 and 60.");

        RuleFor(x => x.Summary)
            .NotEmpty().WithMessage("Summary is required.")
            .MaximumLength(200).WithMessage("Summary must not exceed 200 characters.");
    }
}
