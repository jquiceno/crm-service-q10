using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.LossReasons;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.LossReasons;

/// <summary>
/// Unit tests for LossReasonUsageReader.
///
/// The happy-path branches (cause in use / cause free) require real SQL Server
/// support for keyless entities, so they are covered in the integration tests
/// (F5.1) using Testcontainers, as established by the Fase 2 testing strategy
/// in the workplan.
///
/// This file covers the failure branch, which can be exercised without a live
/// database: a disposed context triggers ObjectDisposedException, which the
/// catch clause captures and converts to PersistenceErrors.Failure.
/// </summary>
public sealed class LossReasonUsageReaderTests
{
    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    // -------------------------------------------------------------------------
    // Branch 3: unexpected exception -> IsFailure with PersistenceErrors.Failure
    //
    // A disposed context triggers ObjectDisposedException on the first EF
    // operation. That exception is not OperationCanceledException, so the
    // catch clause fires and returns PersistenceErrors.Failure.
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsUsedAsync_WhenDatabaseThrows_ReturnsFailureWithCorrectOrigin()
    {
        var logger = Substitute.For<ILoggerPort<LossReasonUsageReader>>();
        var context = CreateContext(nameof(IsUsedAsync_WhenDatabaseThrows_ReturnsFailureWithCorrectOrigin));
        await context.DisposeAsync();

        var reader = new LossReasonUsageReader(context, logger);

        var result = await reader.IsUsedAsync(lossReasonId: 1);

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe(nameof(LossReasonUsageReader));
        logger.Received(1).Error(
            Arg.Is<Exception?>(e => e != null),
            Arg.Any<string>(),
            Arg.Any<object[]>());
    }

    // -------------------------------------------------------------------------
    // Guard: OperationCanceledException must NOT be swallowed
    // -------------------------------------------------------------------------

    [Fact]
    public async Task IsUsedAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        await using var context = CreateContext(nameof(IsUsedAsync_WhenCancelled_ThrowsOperationCanceledException));
        var reader = new LossReasonUsageReader(
            context,
            Substitute.For<ILoggerPort<LossReasonUsageReader>>());

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => reader.IsUsedAsync(lossReasonId: 1, cts.Token));
    }
}
