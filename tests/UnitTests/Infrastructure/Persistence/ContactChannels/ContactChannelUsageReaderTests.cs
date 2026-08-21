using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.ContactChannels;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.ContactChannels;

// The reader runs a scalar SQL query, so only a relational provider can answer it. These tests pin
// the failure contract; whether the query itself counts the right rows belongs to the integration
// tests against SQL Server.
public sealed class ContactChannelUsageReaderTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task IsReferencedAsync_WhenTheQueryCannotRun_ReturnsInternalErrorStampedWithTheOrigin()
    {
        using var context = CreateContext(nameof(IsReferencedAsync_WhenTheQueryCannotRun_ReturnsInternalErrorStampedWithTheOrigin));
        var logger = Substitute.For<ILoggerPort<ContactChannelUsageReader>>();
        var sut = new ContactChannelUsageReader(context, logger);

        var result = await sut.IsReferencedAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(nameof(ContactChannelUsageReader));
    }

    [Fact]
    public async Task IsReferencedAsync_WhenTheQueryCannotRun_LogsTheFailure()
    {
        using var context = CreateContext(nameof(IsReferencedAsync_WhenTheQueryCannotRun_LogsTheFailure));
        var logger = Substitute.For<ILoggerPort<ContactChannelUsageReader>>();
        var sut = new ContactChannelUsageReader(context, logger);

        await sut.IsReferencedAsync(7);

        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task IsReferencedAsync_WhenThePersistenceIsGone_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(IsReferencedAsync_WhenThePersistenceIsGone_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelUsageReader>>();
        var sut = new ContactChannelUsageReader(context, logger);
        await context.DisposeAsync();

        var result = await sut.IsReferencedAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
