using Bogus;
using WeatherForecast.Domain.Entities;

namespace UnitTests.TestSupport.Builders;

public sealed class WeatherForecastEntityBuilder
{
    private static readonly Faker Faker = new();

    private Guid _id = Guid.NewGuid();
    private DateTime _date = Faker.Date.Soon();
    private int _temperatureC = Faker.Random.Int(-20, 40);
    private string _summary = Faker.Lorem.Word();

    public WeatherForecastEntityBuilder WithId(Guid id) { _id = id; return this; }
    public WeatherForecastEntityBuilder WithDate(DateTime date) { _date = date; return this; }
    public WeatherForecastEntityBuilder WithTemperatureC(int temperatureC) { _temperatureC = temperatureC; return this; }
    public WeatherForecastEntityBuilder WithSummary(string summary) { _summary = summary; return this; }

    public WeatherForecastEntity Build() => new(_id, _date, _temperatureC, _summary);
}
