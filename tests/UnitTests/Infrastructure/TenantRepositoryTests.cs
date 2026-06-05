using Infrastructure.MasterAccess.Persistence.EntityFramework;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants;
using Infrastructure.MasterAccess.Persistence.EntityFramework.Tenants.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class TenantRepositoryTests
{
    private static MasterAccessDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<MasterAccessDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new MasterAccessDbContext(options);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenTenantExists_ReturnsMappedAggregate()
    {
        using var context = CreateContext(nameof(GetByCodeAsync_WhenTenantExists_ReturnsMappedAggregate));
        context.Tenants.Add(new Tenant { Code = "TENANT01", Database = "db_tenant", ServerDatabase = 2 });
        await context.SaveChangesAsync();
        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>());

        var result = await sut.GetByCodeAsync("TENANT01");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("TENANT01");
        result.Value.Database.ShouldBe("db_tenant");
        result.Value.ServerDatabase.ShouldBe(2);
    }

    [Fact]
    public async Task GetByCodeAsync_WhenTenantDoesNotExist_ReturnsNotFound()
    {
        using var context = CreateContext(nameof(GetByCodeAsync_WhenTenantDoesNotExist_ReturnsNotFound));
        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>());

        var result = await sut.GetByCodeAsync("NONEXISTENT");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("NONEXISTENT");
    }

    [Fact]
    public async Task GetByCodeAsync_WhenCodeDoesNotMatchStoredTenant_ReturnsNotFound()
    {
        using var context = CreateContext(nameof(GetByCodeAsync_WhenCodeDoesNotMatchStoredTenant_ReturnsNotFound));
        context.Tenants.Add(new Tenant { Code = "TENANT01", Database = "db_tenant", ServerDatabase = 1 });
        await context.SaveChangesAsync();
        var sut = new TenantRepository(context, Substitute.For<ILoggerPort<TenantRepository>>());

        var result = await sut.GetByCodeAsync("TENANT02");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("TENANT02");
    }

    [Fact]
    public async Task GetByCodeAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetByCodeAsync_WhenExceptionThrown_ReturnsInternalErrorAndLogs));
        await context.DisposeAsync();
        var logger = Substitute.For<ILoggerPort<TenantRepository>>();
        var sut = new TenantRepository(context, logger);

        var result = await sut.GetByCodeAsync("TENANT01");

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
