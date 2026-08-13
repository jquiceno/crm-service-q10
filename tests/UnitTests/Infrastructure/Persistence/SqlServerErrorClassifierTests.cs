using Infrastructure.Adapters.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence;

/// <summary>
/// <c>SqlException</c> has no public constructor, so only the <c>int</c> overload and the
/// non-<c>SqlException</c> fallbacks are reachable from a unit test.
/// The numbers are spelled out rather than reusing the constants on purpose: the test has to fail if a
/// constant is ever given the wrong number.
/// </summary>
public sealed class SqlServerErrorClassifierTests
{
    [Theory]
    [InlineData(2627)] // primary key violation
    [InlineData(2601)] // unique index violation
    public void IsUniqueViolation_RecognizesTheDuplicateCodes(int number) =>
        SqlServerErrorClassifier.IsUniqueViolation(number).ShouldBeTrue();

    [Theory]
    [InlineData(547)]  // constraint conflict — a duplicate is never reported through this one
    [InlineData(515)]  // not null
    [InlineData(8152)] // truncation
    [InlineData(1205)] // deadlock
    [InlineData(0)]
    public void IsUniqueViolation_RejectsEveryOtherCode(int number) =>
        SqlServerErrorClassifier.IsUniqueViolation(number).ShouldBeFalse();

    // EF wraps whatever the provider threw, and not every provider failure is a SqlException:
    // the caller has to get a plain no, not a crash on the cast.
    [Fact]
    public void IsUniqueViolation_RejectsAnUpdateExceptionThatDoesNotWrapAServerError() =>
        SqlServerErrorClassifier
            .IsUniqueViolation(new DbUpdateException("failed", new InvalidOperationException()))
            .ShouldBeFalse();

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
