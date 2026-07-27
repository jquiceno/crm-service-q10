using System.Net;
using System.Text;
using Infrastructure.MasterAccess.Http.Tenants;
using Infrastructure.MasterAccess.Security;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class TenantResolverServiceClientTests : IDisposable
{
    private const string BaseUrl = "https://mock.tenants.local/api/";
    private const string PlainConnectionString = "Server=srv;Database=acme_db;";

    private readonly List<HttpClient> _clients = [];

    private sealed class StubHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static string OkBody(string encryptedConnectionString) =>
        $$"""{"data":{"entityCode":"acme","dbName":"acme_db","dbConnectionString":"{{encryptedConnectionString}}","encryptionAlgorithm":"aes-256-cbc"},"statusCode":200}""";

    private static AesConnectionStringDecryptor CreateDecryptor() =>
        new(
            Options.Create(new TenantResolverServiceSettings { EncryptionKey = AesTestCrypto.Passphrase }),
            Substitute.For<ILoggerPort<AesConnectionStringDecryptor>>());

    private TenantResolverServiceClient CreateSut(
        StubHandler handler, ICacheStore? cache = null, IConnectionStringDecryptor? decryptor = null)
    {
        // HttpClient owns the handler (disposeHandler defaults to true), so disposing the
        // client disposes the StubHandler too.
        var client = new HttpClient(handler) { BaseAddress = new Uri(BaseUrl) };
        _clients.Add(client);
        return new TenantResolverServiceClient(
            client,
            cache ?? new JsonRoundTripCacheStore(),
            decryptor ?? CreateDecryptor(),
            Options.Create(new TenantResolverServiceSettings { BaseUrl = BaseUrl }),
            Substitute.For<ILoggerPort<TenantResolverServiceClient>>());
    }

    public void Dispose()
    {
        foreach (var client in _clients)
            client.Dispose();
    }

    [Fact]
    public async Task GetByCodeAsync_WhenEndpointReturns200_DecryptsConnectionString()
    {
        var cipher = AesTestCrypto.Encrypt(PlainConnectionString);
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(cipher)));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsSuccess.ShouldBeTrue();
        result.Value.EntityCode.ShouldBe("acme");
        result.Value.DbName.ShouldBe("acme_db");
        result.Value.ConnectionString.ShouldBe(PlainConnectionString);
    }

    [Fact]
    public async Task GetByCodeAsync_EscapesCodeInRequestUri()
    {
        var cipher = AesTestCrypto.Encrypt(PlainConnectionString);
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(cipher)));
        var sut = CreateSut(handler);

        await sut.GetByCodeAsync("a b/c");

        handler.LastRequest!.RequestUri!.AbsoluteUri
            .ShouldBe($"{BaseUrl}a%20b%2Fc");
    }

    [Fact]
    public async Task GetByCodeAsync_WhenEndpointReturns404_ReturnsNotFoundError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("missing");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("missing");
    }

    [Fact]
    public async Task GetByCodeAsync_WhenEndpointReturns500_ReturnsInternalError()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenPayloadHasEmptyConnectionString_ReturnsInternalError()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK,
            """{"data":{"dbConnectionString":""},"statusCode":200}"""));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenEncryptionAlgorithmUnsupported_ReturnsInternalError()
    {
        var cipher = AesTestCrypto.Encrypt(PlainConnectionString);
        var body = $$"""{"data":{"dbConnectionString":"{{cipher}}","encryptionAlgorithm":"aes-128-cbc"},"statusCode":200}""";
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, body));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenConnectionStringNotDecryptable_ReturnsInternalError()
    {
        // Encrypted with a different passphrase than the SUT's decryptor uses.
        var cipher = AesTestCrypto.Encrypt(PlainConnectionString, "a-different-passphrase");
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(cipher)));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetByCodeAsync_WhenCodeIsBlank_ReturnsValidationError(string code)
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(AesTestCrypto.Encrypt(PlainConnectionString))));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync(code);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenCodeContainsColon_ReturnsValidationErrorWithoutHittingEndpoint()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(AesTestCrypto.Encrypt(PlainConnectionString))));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme:prod");

        // ':' is the cache-key separator: it must be rejected as caller input (Validation), not
        // leak through CacheKey.Resource's ArgumentException into an InternalError.
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenRequestTimesOut_ReturnsInternalError()
    {
        // HttpClient.Timeout surfaces as a TaskCanceledException whose token is NOT the caller's.
        var handler = new StubHandler(_ => throw new TaskCanceledException("timeout"));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenHttpRequestExceptionThrown_ReturnsInternalError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        var sut = CreateSut(handler);

        var result = await sut.GetByCodeAsync("acme");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenCancelled_PropagatesOperationCanceledException()
    {
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(AesTestCrypto.Encrypt(PlainConnectionString))));
        var sut = CreateSut(handler);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = () => sut.GetByCodeAsync("acme", cts.Token);

        await Should.ThrowAsync<OperationCanceledException>(act);
    }

    [Fact]
    public async Task GetByCodeAsync_SecondCall_ServesFromCacheWithoutHittingEndpoint()
    {
        var cipher = AesTestCrypto.Encrypt(PlainConnectionString);
        var handler = new StubHandler(_ => Json(HttpStatusCode.OK, OkBody(cipher)));
        var cache = new JsonRoundTripCacheStore();
        var sut = CreateSut(handler, cache);

        var first = await sut.GetByCodeAsync("acme");
        var second = await sut.GetByCodeAsync("acme");

        first.IsSuccess.ShouldBeTrue();
        second.IsSuccess.ShouldBeTrue();
        // Decrypted on egress on the cache hit too.
        second.Value.ConnectionString.ShouldBe(PlainConnectionString);
        handler.CallCount.ShouldBe(1);
        cache.Keys.ShouldContain("ctx:masteraccess:v1:tenant:acme");
    }
}
