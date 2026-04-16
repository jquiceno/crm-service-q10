using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ServiceTemplate.Tests.Api.Doubles;
using WeatherForecast.Application.UseCases.GetWeatherForecast;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// Spins up the real API and wraps <see cref="IGetWeatherForecastUseCase"/> in a
/// counting decorator so output-cache behaviour can be asserted end-to-end.
/// Each test gets its own factory, its own in-memory output-cache store.
/// </summary>
internal sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private CountingGetWeatherForecastUseCase? _counter;

    public CountingGetWeatherForecastUseCase GetUseCase =>
        _counter ?? throw new InvalidOperationException("Factory not built yet. Call CreateClient() first.");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Cache:Enabled", "true");

        builder.ConfigureServices(services =>
        {
            services.AddScoped<GetWeatherForecastUseCase>();

            var realDescriptor = services.Single(d => d.ServiceType == typeof(IGetWeatherForecastUseCase));
            services.Remove(realDescriptor);

            services.AddSingleton<IGetWeatherForecastUseCase>(sp =>
            {
                _counter = new CountingGetWeatherForecastUseCase(sp.GetRequiredService<IServiceScopeFactory>());
                return _counter;
            });
        });
    }
}
