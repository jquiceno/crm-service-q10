using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Tenants.Aggregates;
using Shared.Results;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class TenantRepositoryCacheTests
{
    [Fact]
    public async Task GetByCodeAsync_UsesCanonicalCacheKey()
    {
        var options = new DbContextOptionsBuilder<MasterAccessDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new MasterAccessDbContext(options);

        var cache = Substitute.For<ICacheStore>();
        var expected = Result<TenantAggregate>.Failure(Shared.Domain.Tenants.Errors.TenantErrors.NotFound("acme"));
        cache.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<Func<Task<Result<TenantAggregate>>>>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(expected));

        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>(), cache);

        await sut.GetByCodeAsync("acme");

        await cache.Received(1).GetOrSetAsync(
            "ctx:masteraccess:v1:tenant:acme",
            TimeSpan.FromMinutes(10),
            Arg.Any<Func<Task<Result<TenantAggregate>>>>(),
            Arg.Any<CancellationToken>());
    }
}
