using Shared.Domain.Errors;
using Shared.Domain.ValueObjects;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Domain.ValueObjects;

public sealed class DateRangeTests
{
    [Fact]
    public void Create_WithoutStartAndEndDate_ReturnsSuccess()
    {
        var result = DateRange.Create(start: null, end: null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Start.ShouldBeNull();
        result.Value.End.ShouldBeNull();
    }

    [Fact]
    public void Create_WithStartDateAndNoEndDate_ReturnsStartDateWithoutEndDate()
    {
        var result = DateRange.Create(new DateTime(2030, 6, 1), end: null);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(DateRangeErrors.StartDateWithoutEndDate);
    }

    [Fact]
    public void Create_WithEndDateAndNoStartDate_ReturnsEndDateWithoutStartDate()
    {
        var result = DateRange.Create(start: null, new DateTime(2030, 6, 30));

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(DateRangeErrors.EndDateWithoutStartDate);
    }

    [Fact]
    public void Create_WithEndDateAfterStartDate_ReturnsSuccess()
    {
        var startDate = new DateTime(2030, 6, 1);
        var endDate = new DateTime(2030, 6, 30);

        var result = DateRange.Create(startDate, endDate);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Start.ShouldBe(startDate);
        result.Value.End.ShouldBe(endDate);
    }

    [Fact]
    public void Create_WithEndDateEqualToStartDate_ReturnsSuccess()
    {
        var date = new DateTime(2030, 6, 1);

        var result = DateRange.Create(date, date);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Start.ShouldBe(date);
        result.Value.End.ShouldBe(date);
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ReturnsEndBeforeStart()
    {
        var startDate = new DateTime(2030, 6, 10);
        var endDate = new DateTime(2030, 6, 5);

        var result = DateRange.Create(startDate, endDate);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(DateRangeErrors.EndBeforeStart);
    }
}
