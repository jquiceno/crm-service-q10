using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherForecast.Domain.Entities;

namespace Infrastructure.Persistence.EntityFramework.WeatherForecast.Configurations;

public sealed class WeatherForecastEntityConfiguration : IEntityTypeConfiguration<WeatherForecastEntity>
{
    public void Configure(EntityTypeBuilder<WeatherForecastEntity> builder)
    {
        builder.ComplexProperty(e => e.Temperature, t =>
        {
            t.Property(x => x.Celsius).HasColumnName("TemperatureC");
        });
    }
}
