using Api.Middleware;
using Api.Session;
using Infrastructure.MasterAccess.Http.Tenants;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Api.Middleware;

public sealed class TenantMiddlewareTests
{
    private const string Code = "acme";
    private const string DbName = "acme_db";
    private const string ConnectionString = "Server=srv;Database=acme_db;";

    private readonly ITenantResolverServiceClient _client = Substitute.For<ITenantResolverServiceClient>();
    private readonly ILoggerPort<TenantMiddleware> _logger = Substitute.For<ILoggerPort<TenantMiddleware>>();

    private static DefaultHttpContext BuildContext(
        string method = "GET",
        string path = "/service-info",
        string? queryCode = null,
        string? headerCode = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();

        if (queryCode is not null)
            context.Request.Query = new QueryCollection(
                new Dictionary<string, StringValues> { ["EntityCode"] = queryCode });

        if (headerCode is not null)
            context.Request.Headers["X-Entity-Code"] = headerCode;

        return context;
    }

    private (TenantMiddleware Sut, bool[] NextCalled) BuildSut()
    {
        var nextCalled = new[] { false };
        var sut = new TenantMiddleware(
            _ =>
            {
                nextCalled[0] = true;
                return Task.CompletedTask;
            },
            _logger);
        return (sut, nextCalled);
    }

    [Theory]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    public async Task InvokeAsync_OnExcludedPath_CallsNextAndSkipsResolution(string path)
    {
        var context = BuildContext(path: path);
        var (sut, nextCalled) = BuildSut();
        var session = new TenantContext();

        await sut.InvokeAsync(context, _client, session);

        nextCalled[0].ShouldBeTrue();
        await _client.DidNotReceiveWithAnyArgs().GetByCodeAsync(default!, default);
    }

    [Fact]
    public async Task InvokeAsync_OnOptionsPreflight_CallsNextAndSkipsResolution()
    {
        var context = BuildContext(method: "OPTIONS");
        var (sut, nextCalled) = BuildSut();

        await sut.InvokeAsync(context, _client, new TenantContext());

        nextCalled[0].ShouldBeTrue();
        await _client.DidNotReceiveWithAnyArgs().GetByCodeAsync(default!, default);
    }

    [Fact]
    public async Task InvokeAsync_WhenCodeMissing_Returns400AndDoesNotCallNext()
    {
        var context = BuildContext();
        var (sut, nextCalled) = BuildSut();

        await sut.InvokeAsync(context, _client, new TenantContext());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        nextCalled[0].ShouldBeFalse();
        await _client.DidNotReceiveWithAnyArgs().GetByCodeAsync(default!, default);
    }

    [Fact]
    public async Task InvokeAsync_WhenTenantNotFound_Returns404AndDoesNotCallNext()
    {
        var context = BuildContext(queryCode: Code);
        _client.GetByCodeAsync(Code, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<TenantInfo>>(
                new NotFoundError($"Tenant with code '{Code}' was not found.")));
        var (sut, nextCalled) = BuildSut();

        await sut.InvokeAsync(context, _client, new TenantContext());

        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        nextCalled[0].ShouldBeFalse();
    }

    [Fact]
    public async Task InvokeAsync_WhenResolvedViaQuery_InitializesSessionAndCallsNext()
    {
        var context = BuildContext(queryCode: Code);
        _client.GetByCodeAsync(Code, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<TenantInfo>>(new TenantInfo(Code, DbName, ConnectionString)));
        var (sut, nextCalled) = BuildSut();
        var session = new TenantContext();

        await sut.InvokeAsync(context, _client, session);

        nextCalled[0].ShouldBeTrue();
        session.ConnectionString.ShouldBe(ConnectionString);
        // The resolved (non-secret) identifiers are logged for observability.
        _logger.Received(1).Debug(Arg.Any<string>(), Code, DbName);
    }

    [Fact]
    public async Task InvokeAsync_WhenResolvedViaHeader_InitializesSession()
    {
        var context = BuildContext(headerCode: Code);
        _client.GetByCodeAsync(Code, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Result<TenantInfo>>(new TenantInfo(Code, DbName, ConnectionString)));
        var (sut, _) = BuildSut();
        var session = new TenantContext();

        await sut.InvokeAsync(context, _client, session);

        session.ConnectionString.ShouldBe(ConnectionString);
    }
}
