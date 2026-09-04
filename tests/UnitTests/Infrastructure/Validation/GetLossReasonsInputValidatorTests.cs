using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class GetLossReasonsInputValidatorTests
{
    private const string ExpectedMessage = "Search text must not exceed 50 characters.";

    private static string SearchOfMaxLength => new('a', LossReasonAggregate.NameMaxLength);

    private static string SearchLongerThanMax => new('a', LossReasonAggregate.NameMaxLength + 1);

    private readonly GetLossReasonsInputValidator _sut = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Price")]
    public void Validate_WithAnySearchUpToTheLimit_ReturnsValid(string? search)
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(search, IsActive: null));

        // A blank search means "do not filter" on the listing, so unlike create and update there is
        // no NotEmpty rule here: requiring it would turn the unfiltered listing into a 400.
        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void Validate_WithAnyIsActive_ReturnsValid(bool? isActive)
    {
        var result = _sut.Validate(new GetLossReasonsInputDto("Price", isActive));

        // D9: the state filter is optional; null means every loss reason.
        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchOfMaxLength_ReturnsValid()
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(SearchOfMaxLength, IsActive: null));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validate_WithSearchLongerThanMax_ReportsItsOwnMessage()
    {
        var result = _sut.Validate(new GetLossReasonsInputDto(SearchLongerThanMax, IsActive: null));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(GetLossReasonsInputDto.Search));

        // The filter carries a plain request-level message, not a domain error: a too-long search is
        // a malformed request, not a broken invariant of the catalog. The literal is spelled out on
        // purpose so a change to the interpolated message cannot pass unnoticed.
        failure.ErrorMessage.ShouldBe(ExpectedMessage);
        failure.CustomState.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_ThroughTheAdapter_ReportsTheFailureOnSearch()
    {
        var adapter = new FluentRequestValidationAdapter<GetLossReasonsInputDto>(_sut);

        var result = await adapter.ValidateAsync(new GetLossReasonsInputDto(SearchLongerThanMax, IsActive: null));

        result.IsFailure.ShouldBeTrue();
        var search = result.Error.Details.Single(d => d.Property == nameof(GetLossReasonsInputDto.Search));
        search.Errors.ShouldNotBeNull();
        search.Errors!.ShouldContain(ExpectedMessage);

        // No domain error behind it, so nothing rebuilds an Attributes dictionary here.
        search.Attributes.ShouldBeNull();
    }
}
