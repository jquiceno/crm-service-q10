using Infrastructure.Adapters.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence;

/// <summary>
/// <c>SqlException</c> has no public constructor, so only the non-<c>SqlException</c> fallback
/// is reachable from a unit test.
/// </summary>
public sealed class SqlServerErrorClassifierTests
{
    [Fact]
    public void Classify_FallsBackToAPersistenceFailureWhenTheInnerExceptionIsNotAServerError()
    {
        var error = SqlServerErrorClassifier.Classify(
            new DbUpdateException("failed", new InvalidOperationException()),
            "MyRepository");

        error.Type.ShouldBe(ErrorType.Internal);
        error.Origin.ShouldBe("MyRepository", "the caller has to be identifiable in the log");
    }
}
