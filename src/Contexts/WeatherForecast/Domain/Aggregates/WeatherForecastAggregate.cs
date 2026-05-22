using Shared.Domain.Aggregates;
using Shared.Domain.Errors;
using Shared.Domain.Result;
using Shared.Domain.ValueObjects;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Aggregates;

public sealed class WeatherForecastAggregate : AggregateRoot<WeatherForecastEntity, Guid>
{
    public const int MaxSummaryLength = 200;

    public DateTime Date => Entity.Date;
    public int TemperatureCelsius => Entity.Temperature.Celsius;
    public int TemperatureFahrenheit => Entity.Temperature.Fahrenheit;
    public string Summary => Entity.Summary;
    public DateTime CreatedAtUtc => Entity.CreatedAtUtc;
    public string? AddressStreet => Entity.Address?.Street;
    public string? AddressCity => Entity.Address?.City;
    public string? AddressZipCode => Entity.Address?.ZipCode;

    private WeatherForecastAggregate(WeatherForecastEntity entity) : base(entity) { }

    public static Result<WeatherForecastAggregate> Create(
        Guid id, DateTime date, int temperature, string summary,
        string? street = null, string? city = null, string? zipCode = null)
    {
        var errors = new List<ValidationError>();

        var temperatureResult = Temperature.Create(temperature);
        if (temperatureResult.IsFailure)
            errors.Add(temperatureResult.TypedError with { Property = nameof(Temperature), Value = temperature });

        if (id == Guid.Empty)
        {
            errors.Add(new ValidationError("Id is required.", ErrorType.Validation)
            {
                Property = nameof(id),
                Value = id
            });
        }

        Address? address = null;
        if (street is not null || city is not null || zipCode is not null)
        {
            var addressResult = Address.Create(street, city, zipCode);
            if (addressResult.IsFailure)
                errors.Add(new ValidationError("Address is invalid.", ErrorType.Validation)
                {
                    Property = nameof(Address),
                    Value = new { street, city, zipCode },
                    Children = addressResult.TypedError.Errors
                });
            else
                address = addressResult.Value;
        }

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var entity = new WeatherForecastEntity(id, date, temperatureResult.Value, summary, address);
        return new WeatherForecastAggregate(entity);
    }

    public WeatherForecastEntity ToEntity() => Entity;

    public static WeatherForecastAggregate FromEntity(WeatherForecastEntity entity)
        => new(entity);
}
