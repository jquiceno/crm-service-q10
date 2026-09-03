using System.Diagnostics;
using Infrastructure.Observability;
using IntegrationTests.Caching;
using Shouldly;
using Xunit;

namespace IntegrationTests.Infrastructure;

/// <summary>
/// Verifica el trace id de punta a punta contra <c>/health/live</c>, que no toca persistencia.
/// Corre sobre el <c>ApiFactory</c> del suite, que limpia los proveedores de logging
/// (ClearProviders): así se demuestra que el <c>Activity</c> de la petición se crea de forma nativa
/// mientras el logging del host esté habilitado.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class TraceContextTests : IntegrationTestBase
{
    public TraceContextTests(SqlServerContainerFixture fixture, RedisContainerFixture cache)
        : base(fixture, cache) { }

    private string HealthLive => $"/{RoutePrefix}/health/live";

    [Fact]
    public async Task Response_Exposes_TraceId_Header_With_32_Hex_Chars()
    {
        var response = await Client.GetAsync(HealthLive);

        response.Headers.TryGetValues(TraceHeaders.TraceId, out var values).ShouldBeTrue();
        var traceId = values!.Single();
        traceId.Length.ShouldBe(32);
        traceId.ShouldAllBe(c => Uri.IsHexDigit(c));
    }

    [Fact]
    public async Task Incoming_Traceparent_Is_Continued_As_Same_TraceId()
    {
        var incomingTraceId = ActivityTraceId.CreateRandom().ToString();
        var incomingSpanId = ActivitySpanId.CreateRandom().ToString();
        var traceparent = $"00-{incomingTraceId}-{incomingSpanId}-01";

        using var request = new HttpRequestMessage(HttpMethod.Get, HealthLive);
        request.Headers.TryAddWithoutValidation("traceparent", traceparent);

        var response = await Client.SendAsync(request);

        var traceId = response.Headers.GetValues(TraceHeaders.TraceId).Single();
        traceId.ShouldBe(incomingTraceId);
    }

    [Fact]
    public async Task First_Service_Generates_New_TraceId_Per_Request()
    {
        var first = await Client.GetAsync(HealthLive);
        var second = await Client.GetAsync(HealthLive);

        var firstTraceId = first.Headers.GetValues(TraceHeaders.TraceId).Single();
        var secondTraceId = second.Headers.GetValues(TraceHeaders.TraceId).Single();

        firstTraceId.ShouldNotBe(secondTraceId);
    }
}
