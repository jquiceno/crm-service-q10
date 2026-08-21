using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Errors;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Domain;

public sealed class BusinessStatusAggregateTests
{
    private static CreateBusinessStatusArgs CreateArgs(
        string? name = "Negotiation",
        decimal percentage = 50m,
        string? color = "49ff7c",
        bool isActive = true) =>
        new(name, percentage, color, isActive);

    private static UpdateBusinessStatusArgs UpdateArgs(
        string? name = "Negotiation",
        decimal percentage = 50m,
        string? color = "49ff7c",
        bool isActive = true) =>
        new(name, percentage, color, isActive);

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidArgs_ReturnsAggregate()
    {
        var result = BusinessStatusAggregate.Create(CreateArgs());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Negotiation");
        result.Value.Percentage.ShouldBe(50);
        result.Value.Color!.Value.ShouldBe("49ff7c");
        result.Value.IsActive.ShouldBeTrue();
        result.Value.IsIntermediate.ShouldBeTrue();
        result.Value.IsTerminal.ShouldBeFalse();
        result.Value.CreatedAt.ShouldNotBeNull();
        result.Value.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Create_DoesNotAssignIdentity()
    {
        var result = BusinessStatusAggregate.Create(CreateArgs());

        result.Value.Id.ShouldBe(0);
    }

    [Fact]
    public void Create_TrimsTheName()
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(name: "  Negotiation  "));

        result.Value.Name.ShouldBe("Negotiation");
    }

    [Fact]
    public void Create_WithEmptyNameAndTerminalPercentage_ReturnsBothErrors()
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(name: "   ", percentage: 100m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        var properties = result.Error.Details.Select(detail => detail.Property).ToList();
        properties.Count.ShouldBe(2);
        properties.ShouldContain("Name");
        properties.ShouldContain("Percentage");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutName_ReturnsNameRequired(string? name)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(name: name));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.NameRequired, "Name");
    }

    [Fact]
    public void Create_WithNameLongerThanTheMaximum_ReturnsNameTooLong()
    {
        var name = new string('a', BusinessStatusAggregate.MaxNameLength + 1);

        var result = BusinessStatusAggregate.Create(CreateArgs(name: name));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.NameTooLong, "Name");
    }

    [Fact]
    public void Create_WithNameAtTheMaximum_ReturnsAggregate()
    {
        var name = new string('a', BusinessStatusAggregate.MaxNameLength);

        var result = BusinessStatusAggregate.Create(CreateArgs(name: name));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.Length.ShouldBe(BusinessStatusAggregate.MaxNameLength);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Create_WithPercentageOutOfRange_ReturnsPercentageOutOfRange(int percentage)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(percentage: percentage));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.PercentageOutOfRange, "Percentage");
    }

    [Theory]
    [InlineData(50.5)]
    [InlineData(99.9)]
    [InlineData(0.4)]
    public void Create_WithNonIntegerPercentage_ReturnsPercentageMustBeInteger(decimal percentage)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(percentage: percentage));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.PercentageMustBeInteger, "Percentage");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Create_WithTerminalPercentage_ReturnsTerminalPercentageNotAllowed(int percentage)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(percentage: percentage));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.TerminalPercentageNotAllowed, "Percentage");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(99)]
    public void Create_WithPercentageNextToTheReservedLimits_ReturnsAggregate(int percentage)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(percentage: percentage));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Percentage.ShouldBe(percentage);
    }

    [Fact]
    public void Create_WithMalformedColor_ReturnsInvalidColorFormat()
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(color: "zzzzzz"));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.InvalidColorFormat, "Color");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutColor_LeavesTheColorAbsent(string? color)
    {
        var result = BusinessStatusAggregate.Create(CreateArgs(color: color));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Color.ShouldBeNull();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithValidArgs_ReplacesEveryField()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(name: "Proposal", percentage: 60m, color: "AABBCC", isActive: false));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("Proposal");
        aggregate.Percentage.ShouldBe(60);
        aggregate.Color!.Value.ShouldBe("AABBCC");
        aggregate.IsActive.ShouldBeFalse();
        aggregate.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Update_WithoutColor_ClearsTheColor()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(color: null));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Color.ShouldBeNull();
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(100, 50)]
    [InlineData(0, 100)]
    public void Update_OnATerminalChangingThePercentage_ReturnsTerminalPercentageIsImmutable(
        int stored, int incoming)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Won", stored, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(percentage: incoming));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.TerminalPercentageIsImmutable, "Percentage");
        aggregate.Percentage.ShouldBe(stored);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Update_OnATerminalKeepingThePercentage_UpdatesNameAndColor(int stored)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Won", stored, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(name: "Closed won", percentage: stored, color: "AABBCC"));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("Closed won");
        aggregate.Color!.Value.ShouldBe("AABBCC");
        aggregate.Percentage.ShouldBe(stored);
        aggregate.IsTerminal.ShouldBeTrue();
    }

    [Fact]
    public void Update_OnATerminalWithANonIntegerPercentage_ReturnsPercentageMustBeInteger()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Won", 100, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(percentage: 99.9m));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.PercentageMustBeInteger, "Percentage");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Update_OnAnIntermediateWithATerminalPercentage_ReturnsTerminalPercentageNotAllowed(
        int incoming)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(percentage: incoming));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.TerminalPercentageNotAllowed, "Percentage");
        aggregate.Percentage.ShouldBe(50);
    }

    [Fact]
    public void Update_WithNameLongerThanTheMaximum_ReturnsNameTooLong()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(name: new string('a', BusinessStatusAggregate.MaxNameLength + 1)));

        ShouldBeSingleValidationError(result, BusinessStatusErrors.NameTooLong, "Name");
        aggregate.Name.ShouldBe("Negotiation");
    }

    [Fact]
    public void Update_WithEmptyNameAndMalformedColor_ReturnsBothErrors()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        var result = aggregate.Update(UpdateArgs(name: null, color: "zzzzzz"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        var properties = result.Error.Details.Select(detail => detail.Property).ToList();
        properties.Count.ShouldBe(2);
        properties.ShouldContain("Name");
        properties.ShouldContain("Color");
    }

    [Fact]
    public void Update_OnAStatusWithoutPercentage_AppliesTheCreationRules()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Legacy", percentage: null, "49ff7c", true);

        aggregate.IsTerminal.ShouldBeFalse();
        var result = aggregate.Update(UpdateArgs(percentage: 30m));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Percentage.ShouldBe(30);
    }

    // ── EnsureCanBeDeleted ────────────────────────────────────────────────────

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void EnsureCanBeDeleted_OnATerminal_ReturnsTerminalCannotBeDeleted(int percentage)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Terminal", percentage, null, true);

        var result = aggregate.EnsureCanBeDeleted();

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(BusinessStatusErrors.TerminalCannotBeDeleted);
        result.Error.Type.ShouldBe(ErrorType.Conflict);
    }

    [Theory]
    [InlineData(50)]
    [InlineData(null)]
    public void EnsureCanBeDeleted_OnANonTerminal_ReturnsSuccess(int? percentage)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", percentage, null, true);

        aggregate.EnsureCanBeDeleted().IsSuccess.ShouldBeTrue();
    }

    // ── Terminal semantics ────────────────────────────────────────────────────

    [Theory]
    [InlineData(100, true, false)]
    [InlineData(0, false, true)]
    [InlineData(50, false, false)]
    [InlineData(null, false, false)]
    public void TerminalSemantics_AreDecidedByExactEquality(int? percentage, bool isWon, bool isLost)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Stage", percentage, null, true);

        aggregate.IsWon.ShouldBe(isWon);
        aggregate.IsLost.ShouldBe(isLost);
        aggregate.IsTerminal.ShouldBe(isWon || isLost);
        aggregate.IsIntermediate.ShouldBe(!isWon && !isLost);
    }

    // ── Reconstruct ───────────────────────────────────────────────────────────

    [Fact]
    public void Reconstruct_WithNullFields_DoesNotThrow()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, null!, percentage: null, color: null, isActive: false);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBeNull();
        aggregate.Percentage.ShouldBeNull();
        aggregate.Color.ShouldBeNull();
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Reconstruct_DoesNotStampAuditDates()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "49ff7c", true);

        aggregate.CreatedAt.ShouldBeNull();
        aggregate.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Reconstruct_DoesNotValidateTheStoredName()
    {
        var name = new string('a', BusinessStatusAggregate.MaxNameLength + 1);

        var aggregate = BusinessStatusAggregate.Reconstruct(7, name, 50, null, true);

        aggregate.Name.ShouldBe(name);
    }

    [Fact]
    public void Reconstruct_DoesNotValidateTheStoredColor()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, "zzzzzz", true);

        aggregate.Color!.Value.ShouldBe("zzzzzz");
    }

    [Fact]
    public void Reconstruct_WithAnEmptyColor_LeavesTheColorAbsent()
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Negotiation", 50, string.Empty, true);

        aggregate.Color.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Reconstruct_DoesNotRejectAStoredTerminalPercentage(int percentage)
    {
        var aggregate = BusinessStatusAggregate.Reconstruct(7, "Terminal", percentage, null, true);

        aggregate.Percentage.ShouldBe(percentage);
        aggregate.IsTerminal.ShouldBeTrue();
    }

    private static void ShouldBeSingleValidationError(
        Result result, ValidationError expected, string property)
    {
        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.Count.ShouldBe(1);
        result.Error.Details[0].Property.ShouldBe(property);
        result.Error.Details[0].Errors!.ShouldContain(expected.Message);
    }
}
