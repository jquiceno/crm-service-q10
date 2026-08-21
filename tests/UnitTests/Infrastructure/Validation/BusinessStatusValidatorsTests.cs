using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Application.UseCases.GetBusinessStatuses;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Enums;
using Infrastructure.Validation.FluentValidation.BusinessStatuses;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class BusinessStatusValidatorsTests
{
    private readonly CreateBusinessStatusInputValidator _createValidator = new();

    private readonly GetBusinessStatusesInputValidator _listValidator = new();

    private static CreateBusinessStatusInputDto CreateInput(
        string? name = "Negotiation",
        decimal percentage = 50m,
        string? color = "49ff7c",
        bool isActive = true) =>
        new(name, percentage, color, isActive);

    [Fact]
    public void CreateValidator_WithValidInput_ReturnsValid()
    {
        var result = _createValidator.Validate(CreateInput());

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateValidator_WithBlankName_HasErrorOnName(string? name)
    {
        var result = _createValidator.Validate(CreateInput(name: name));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBusinessStatusInputDto.Name));
    }

    [Fact]
    public void CreateValidator_WithNameOverTheMaximumLength_HasErrorOnName()
    {
        var result = _createValidator.Validate(
            CreateInput(name: new string('a', BusinessStatusAggregate.MaxNameLength + 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBusinessStatusInputDto.Name));
    }

    [Fact]
    public void CreateValidator_WithNameAtTheMaximumLength_ReturnsValid()
    {
        var result = _createValidator.Validate(
            CreateInput(name: new string('a', BusinessStatusAggregate.MaxNameLength)));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void CreateValidator_WithPercentageOutOfRange_HasErrorOnPercentage(int percentage)
    {
        var result = _createValidator.Validate(CreateInput(percentage: percentage));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBusinessStatusInputDto.Percentage));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void CreateValidator_WithTerminalPercentage_ReturnsValid(int percentage)
    {
        // INV-1 belongs to the aggregate, not to the structural layer: the client must get the domain
        // error naming the rule, not a generic range failure.
        var result = _createValidator.Validate(CreateInput(percentage: percentage));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateValidator_WithNonIntegerPercentageInRange_ReturnsValid()
    {
        // PercentageMustBeInteger is a domain error too.
        var result = _createValidator.Validate(CreateInput(percentage: 50.5m));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("#49ff7c")]
    [InlineData("49ff7")]
    [InlineData("49ff7cc")]
    [InlineData("zzzzzz")]
    public void CreateValidator_WithMalformedColor_HasErrorOnColor(string color)
    {
        var result = _createValidator.Validate(CreateInput(color: color));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateBusinessStatusInputDto.Color));
    }

    [Theory]
    [InlineData("49ff7c")]
    [InlineData("49FF7C")]
    [InlineData(null)]
    [InlineData("")]
    public void CreateValidator_WithAcceptedColor_ReturnsValid(string? color)
    {
        var result = _createValidator.Validate(CreateInput(color: color));

        result.IsValid.ShouldBeTrue();
    }

    // ── GetBusinessStatusesInputValidator ─────────────────────────────────────

    [Fact]
    public void ListValidator_WithEveryFilterOmitted_ReturnsValid()
    {
        var result = _listValidator.Validate(new GetBusinessStatusesInputDto());

        result.IsValid.ShouldBeTrue("every filter of the listing is optional");
    }

    [Theory]
    [InlineData(BusinessStatusKind.All)]
    [InlineData(BusinessStatusKind.Intermediate)]
    [InlineData(BusinessStatusKind.Terminal)]
    public void ListValidator_WithADeclaredKind_ReturnsValid(BusinessStatusKind kind)
    {
        var result = _listValidator.Validate(new GetBusinessStatusesInputDto(Kind: kind));

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ListValidator_WithAKindOutsideTheEnum_HasErrorOnKind()
    {
        var result = _listValidator.Validate(new GetBusinessStatusesInputDto(Kind: (BusinessStatusKind)99));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetBusinessStatusesInputDto.Kind));
    }

    [Fact]
    public void ListValidator_WithNameOverTheMaximumLength_HasErrorOnName()
    {
        var result = _listValidator.Validate(
            new GetBusinessStatusesInputDto(new string('a', BusinessStatusAggregate.MaxNameLength + 1)));

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == nameof(GetBusinessStatusesInputDto.Name));
    }

    [Fact]
    public void ListValidator_WithNameAtTheMaximumLength_ReturnsValid()
    {
        var result = _listValidator.Validate(
            new GetBusinessStatusesInputDto(new string('a', BusinessStatusAggregate.MaxNameLength)));

        result.IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void ListValidator_DoesNotRejectATerminalName_NorConstrainThePercentage(int percentage)
    {
        // The listing has no percentage field: filtering terminals is what Kind is for, and INV-1
        // only governs writes.
        var result = _listValidator.Validate(
            new GetBusinessStatusesInputDto(percentage.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        result.IsValid.ShouldBeTrue();
    }
}
