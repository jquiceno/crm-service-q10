using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using WeatherForecast.Domain.Entities;

namespace Infrastructure.Persistence.EntityFramework.WeatherForecast.Configurations;

public sealed class WeatherForecastEntityConfiguration : IEntityTypeConfiguration<WeatherForecastEntity>
{
    public void Configure(EntityTypeBuilder<WeatherForecastEntity> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.OwnsOne(e => e.Temperature, t =>
        {
            t.Property(x => x.Celsius).HasColumnName("TemperatureC");
            t.Ignore(x => x.Fahrenheit);
        });

        builder.OwnsOne(e => e.Address, a =>
        {
            a.Property(x => x.Street).HasColumnName("AddressStreet");
            a.Property(x => x.City).HasColumnName("AddressCity");
            a.Property(x => x.ZipCode).HasColumnName("AddressZipCode");
        });

        //Here would go the Fluent API (EF) configuration.
    }
}
