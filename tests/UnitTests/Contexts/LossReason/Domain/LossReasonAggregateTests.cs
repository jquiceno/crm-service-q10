using LossReason.Domain.Aggregates;
using LossReason.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.LossReason.Domain;

public sealed class LossReasonAggregateTests
{
    private const string ValidName = "Precio";

    private static string NameOfMaxLength => new('a', LossReasonAggregate.NameMaxLength);

    private static string NameLongerThanMax => new('a', LossReasonAggregate.NameMaxLength + 1);

    [Fact]
    public void Create_WithValidArgs_ReturnsAggregateWithAuditDates()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(ValidName, IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(ValidName);
        result.Value.IsActive.ShouldBeTrue();
        result.Value.Id.ShouldBe(0);
        result.Value.CreatedAt.ShouldNotBeNull();
        result.Value.UpdatedAt.ShouldNotBeNull();
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsNameRequired()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(string.Empty, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        MessagesOf(result.Error).ShouldBe([LossReasonErrors.NameRequired.Message]);
    }

    [Fact]
    public void Create_WithWhitespaceName_ReturnsNameRequired()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs("   ", IsActive: true));

        result.IsFailure.ShouldBeTrue();
        MessagesOf(result.Error).ShouldBe([LossReasonErrors.NameRequired.Message]);
    }

    [Fact]
    public void Create_WithNameOf51Characters_ReturnsNameTooLong()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(NameLongerThanMax, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        MessagesOf(result.Error).ShouldBe([LossReasonErrors.NameTooLong.Message]);
    }

    [Fact]
    public void Create_WithNameOf50Characters_Succeeds()
    {
        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(NameOfMaxLength, IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(NameOfMaxLength);
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithEmptyAndTooLongName_AccumulatesBothErrors()
    {
        // Whitespace longer than the limit violates both invariants at once.
        var name = new string(' ', LossReasonAggregate.NameMaxLength + 1);

        var result = LossReasonAggregate.Create(new CreateLossReasonArgs(name, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        var messages = MessagesOf(result.Error);
        messages.Count.ShouldBe(2);
        messages.ShouldContain(LossReasonErrors.NameRequired.Message);
        messages.ShouldContain(LossReasonErrors.NameTooLong.Message);
    }

    [Fact]
    public void Update_WithEmptyName_ReturnsNameRequired()
    {
        var aggregate = LossReasonAggregate.Reconstruct(7, ValidName, isActive: true);

        var result = aggregate.Update(new UpdateLossReasonArgs(string.Empty, IsActive: false));

        result.IsFailure.ShouldBeTrue();
        MessagesOf(result.Error).ShouldBe([LossReasonErrors.NameRequired.Message]);
        aggregate.Name.ShouldBe(ValidName);
        aggregate.IsActive.ShouldBeTrue();
        aggregate.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Update_WithNameOf51Characters_ReturnsNameTooLong()
    {
        var aggregate = LossReasonAggregate.Reconstruct(7, ValidName, isActive: true);

        var result = aggregate.Update(new UpdateLossReasonArgs(NameLongerThanMax, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        MessagesOf(result.Error).ShouldBe([LossReasonErrors.NameTooLong.Message]);
        aggregate.Name.ShouldBe(ValidName);
    }

    [Fact]
    public void Update_WithValidArgs_SetsUpdatedAt()
    {
        var aggregate = LossReasonAggregate.Reconstruct(7, ValidName, isActive: true);

        var result = aggregate.Update(new UpdateLossReasonArgs("Competencia", IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("Competencia");
        aggregate.IsActive.ShouldBeFalse();
        aggregate.UpdatedAt.ShouldNotBeNull();
        aggregate.CreatedAt.ShouldBeNull();
    }

    [Fact]
    public void Reconstruct_WithNameLongerThan50_DoesNotValidate()
    {
        var aggregate = LossReasonAggregate.Reconstruct(7, NameLongerThanMax, isActive: true);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBe(NameLongerThanMax);
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Reconstruct_Always_DoesNotSetAuditDates()
    {
        var aggregate = LossReasonAggregate.Reconstruct(7, ValidName, isActive: true);

        aggregate.CreatedAt.ShouldBeNull();
        aggregate.UpdatedAt.ShouldBeNull();
    }

    private static IReadOnlyList<string> MessagesOf(DomainError error) =>
        [.. error.Details.SelectMany(detail => detail.Errors ?? [])];
}
