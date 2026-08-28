using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation.Shared;
using Shared.Application.Dtos;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class IdInputValidatorTests
{
    private const string ExpectedMessage = "Id must be greater than 0.";

    private readonly IdInputValidator _sut = new();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void Validate_WithAPositiveId_ReturnsValid(int id)
    {
        _sut.Validate(new IdInputDto(id)).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void Validate_WithANonPositiveId_ReportsTheFailure(int id)
    {
        var result = _sut.Validate(new IdInputDto(id));

        result.IsValid.ShouldBeFalse();
        var failure = result.Errors.Single(e => e.PropertyName == nameof(IdInputDto.Id));
        failure.ErrorMessage.ShouldBe(ExpectedMessage);
    }

    [Fact]
    public async Task ValidateAsync_ThroughTheAdapter_ReportsTheFailureOnId()
    {
        var adapter = new FluentRequestValidationAdapter<IdInputDto>(_sut);

        var result = await adapter.ValidateAsync(new IdInputDto(0));

        result.IsFailure.ShouldBeTrue();
        var id = result.Error.Details.Single(d => d.Property == nameof(IdInputDto.Id));
        id.Errors.ShouldNotBeNull();
        id.Errors!.ShouldContain(ExpectedMessage);
    }
}
