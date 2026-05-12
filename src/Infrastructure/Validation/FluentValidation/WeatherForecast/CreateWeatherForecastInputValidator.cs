using FluentValidation;
using Shared.Domain.Errors;
using WeatherForecast.Application.UseCases.CreateWeatherForecast;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Errors;
using WeatherForecast.Domain.ValueObjects;

namespace Infrastructure.Validation.FluentValidation.WeatherForecast;

public class CreateWeatherForecastInputValidator : AbstractValidator<CreateWeatherForecastInputDto>, IStructuralValidator<CreateWeatherForecastInputDto>
{
    public CreateWeatherForecastInputValidator()
    {
        RuleFor(x => x.Date)
            .NotEmpty()
            .WithMessage(WeatherForecastErrors.DateRequired.Message)
            .WithState(_ => WeatherForecastErrors.DateRequired);

        RuleFor(x => x.Temperature)
            .InclusiveBetween(Temperature.MinCelsius, Temperature.MaxCelsius)
            .WithMessage(WeatherForecastErrors.TemperatureOutOfRange.Message)
            .WithState(_ => WeatherForecastErrors.TemperatureOutOfRange);

        RuleFor(x => x.Summary)
            .NotEmpty()
            .WithMessage(WeatherForecastErrors.SummaryRequired.Message)
            .WithState(_ => WeatherForecastErrors.SummaryRequired);

        RuleFor(x => x.Summary)
            .MaximumLength(WeatherForecastAggregate.MaxSummaryLength)
            .WithMessage(WeatherForecastErrors.SummaryTooLong.Message)
            .WithState(_ => WeatherForecastErrors.SummaryTooLong);

        When(x => x.Address is not null, () =>
        {
            RuleFor(x => x.Address!.Street)
                .NotEmpty()
                .WithMessage(AddressErrors.StreetRequired.Message)
                .WithState(_ => AddressErrors.StreetRequired);

            RuleFor(x => x.Address!.City)
                .NotEmpty()
                .WithMessage(AddressErrors.CityRequired.Message)
                .WithState(_ => AddressErrors.CityRequired);

            RuleFor(x => x.Address!.ZipCode)
                .NotEmpty()
                .WithMessage(AddressErrors.ZipCodeRequired.Message)
                .WithState(_ => AddressErrors.ZipCodeRequired);
        });
    }
}
