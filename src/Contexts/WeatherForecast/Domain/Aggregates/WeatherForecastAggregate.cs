using Shared.Domain;
using Shared.Domain.ValueObjects;
using WeatherForecast.Domain.Entities;
using WeatherForecast.Domain.ValueObjects;

namespace WeatherForecast.Domain.Aggregates;

public sealed class WeatherForecastAggregate : AggregateRoot<WeatherForecastEntity>
{
    public const int MaxSummaryLength = 200;

    public DateTime Date => _entity.Date;
    public int TemperatureCelsius => _entity.Temperature.Celsius;
    public int TemperatureFahrenheit => _entity.Temperature.Fahrenheit;
    public string Summary => _entity.Summary;
    public DateTime CreatedAtUtc => _entity.CreatedAtUtc;
    public Address? Address => _entity.Address;

    private WeatherForecastAggregate(WeatherForecastEntity entity) : base(entity) { }

    public static Result<WeatherForecastAggregate> Create(
        Guid id, DateTime date, int temperature, string summary,
        string? street = null, string? city = null, string? zipCode = null)
    {
        var errors = new List<ValidationError>();

        var temperatureResult = Temperature.Create(temperature);
        if (temperatureResult.IsFailure)
            errors.Add(temperatureResult.Error with { Property = nameof(Temperature), Value = temperature });

        Address? address = null;
        if (street is not null || city is not null || zipCode is not null)
        {
            var addressResult = Address.Create(street, city, zipCode);
            if (addressResult.IsFailure)
                errors.Add(new ValidationError("Address is invalid.", ErrorType.Validation)
                {
                    Property = nameof(Address),
                    Value = new { street, city, zipCode },
                    Children = addressResult.Error.Errors
                });
            else
                address = addressResult.Value;
        }

        if (errors.Count > 0)
            return DomainError.FromValidationDomainErrors(errors);

        var entity = new WeatherForecastEntity(id, date, temperatureResult.Value, summary, address);
        return new WeatherForecastAggregate(entity);
    }

    public WeatherForecastEntity ToEntity() => _entity;

    public static WeatherForecastAggregate FromEntity(WeatherForecastEntity entity)
        => new(entity);
}
