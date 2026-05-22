using Bogus;
using Shared.Domain.ValueObjects;
using WeatherForecast.Domain.Aggregates;
using WeatherForecast.Domain.Entities;

namespace UnitTests.TestSupport.Builders;

public sealed class WeatherForecastEntityBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private DateTime _date = Faker.Date.Soon();
    private int _temperature = Faker.Random.Int(-20, 40);
    private string _summary = Faker.Lorem.Word();

    private string? _street = null;
    private string? _city = null;
    private string? _zipCode = null;

    public WeatherForecastEntityBuilder WithId(Guid id) { _id = id; return this; }
    public WeatherForecastEntityBuilder WithDate(DateTime date) { _date = date; return this; }
    public WeatherForecastEntityBuilder WithTemperatureC(int temperature) { _temperature = temperature; return this; }
    public WeatherForecastEntityBuilder WithSummary(string summary) { _summary = summary; return this; }

    public WeatherForecastEntity Build()
    {
        var aggregate = WeatherForecastAggregate.Create(Guid.NewGuid(), _date, _temperature, _summary, _street, _city, _zipCode);
        return aggregate.Value.ToEntity();
    }
}
