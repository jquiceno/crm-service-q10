using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServiceTemplate.Tests.Api.Doubles;
using Shared.Application.Interfaces;

namespace ServiceTemplate.Tests.Api;

/// <summary>
/// Spins up the real API with a <see cref="SpyCacheService"/> wired in place of
/// the real <see cref="ICacheService"/>, so filter behaviour can be asserted
/// without a live Redis instance.
/// </summary>
internal sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public SpyCacheService CacheService { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace whatever cache service Program registered (NullCacheService
            // by default in test config) with the spy.
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService>(CacheService);
        });
    }
}
