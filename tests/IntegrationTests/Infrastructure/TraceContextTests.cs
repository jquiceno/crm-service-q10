using System.Diagnostics;
using Infrastructure.Observability;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Verifica el trace id de punta a punta. No depende de base de datos: usa persistencia
/// en memoria, por lo que no requiere Docker. Replica el logging del <c>ApiFactory</c>
/// (ClearProviders) para demostrar que el <c>Activity</c> de la petición se crea de forma
/// nativa mientras el logging del host esté habilitado.
/// </summary>
public sealed class TraceContextTests : IClassFixture<TraceContextTests.TraceApiFactory>
{
    private readonly TraceApiFactory _factory;

    public TraceContextTests(TraceApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Response_Exposes_TraceId_Header_With_32_Hex_Chars()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/service-template/health/live");

        response.Headers.TryGetValues(TraceHeaders.TraceId, out var values).ShouldBeTrue();
        var traceId = values!.Single();
        traceId.Length.ShouldBe(32);
        traceId.ShouldAllBe(c => Uri.IsHexDigit(c));
    }

    [Fact]
    public async Task Incoming_Traceparent_Is_Continued_As_Same_TraceId()
    {
        using var client = _factory.CreateClient();
        var incomingTraceId = ActivityTraceId.CreateRandom().ToString();
        var incomingSpanId = ActivitySpanId.CreateRandom().ToString();
        var traceparent = $"00-{incomingTraceId}-{incomingSpanId}-01";

        using var request = new HttpRequestMessage(HttpMethod.Get, "/service-template/health/live");
        request.Headers.TryAddWithoutValidation("traceparent", traceparent);

        var response = await client.SendAsync(request);

        var traceId = response.Headers.GetValues(TraceHeaders.TraceId).Single();
        traceId.ShouldBe(incomingTraceId);
    }

    [Fact]
    public async Task First_Service_Generates_New_TraceId_Per_Request()
    {
        using var client = _factory.CreateClient();

        var first = await client.GetAsync("/service-template/health/live");
        var second = await client.GetAsync("/service-template/health/live");

        var firstTraceId = first.Headers.GetValues(TraceHeaders.TraceId).Single();
        var secondTraceId = second.Headers.GetValues(TraceHeaders.TraceId).Single();

        firstTraceId.ShouldNotBe(secondTraceId);
    }

    public sealed class TraceApiFactory : WebApplicationFactory<Program>
    {
        public TraceApiFactory()
        {
            Environment.SetEnvironmentVariable("Sentry__Enabled", "false");
            Environment.SetEnvironmentVariable("Sentry__Dsn", string.Empty);
            Environment.SetEnvironmentVariable("SENTRY_DSN", string.Empty);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // Replicate the integration ApiFactory: clean up providers and raise the minimum level.
            // The traceId should still be present without a custom listener, because the host's
            // enabled logging already triggers the creation of the request Activity.
            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Warning);
            });
        }
    }
}
