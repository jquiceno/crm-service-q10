using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.Shared;
using Shared.Application.Dtos;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class SequenceIdInputValidatorTests
{
    private const string ExpectedMessage = "Id must be greater than 0.";

    private readonly SequenceIdInputValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Validate_WithAPositiveId_ReturnsValid(int id)
    {
        _sut.Validate(new SequenceIdInputDto(id)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithANonPositiveId_ReportsTheFailure(int id)
    {
        var result = _sut.Validate(new SequenceIdInputDto(id));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(SequenceIdInputDto.Id));
        failure.ErrorMessage.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task ValidateAsync_ThroughTheAdapter_ReportsTheFailureOnId()
    {
        var adapter = new FluentRequestValidationAdapter<SequenceIdInputDto>(_sut);

        var result = await adapter.ValidateAsync(new SequenceIdInputDto(0));

        result.IsFailure.ShouldBeTrue();
        var id = result.Error.Details.Single(d => d.Property == nameof(SequenceIdInputDto.Id));
        id.Errors.ShouldNotBeNull();
        id.Errors!.ShouldContain(ExpectedMessage);
    }
}
