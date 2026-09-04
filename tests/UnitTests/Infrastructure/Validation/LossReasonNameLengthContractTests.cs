using Infrastructure.Validation.FluentValidation.LossReasons;
using LossReason.Application.UseCases.CreateLossReason;
using LossReason.Application.UseCases.GetLossReasons;
using LossReason.Application.UseCases.UpdateLossReason;
using LossReason.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

/// <summary>
/// D4 duplicates the Name length rule on purpose — FluentValidation rejects it at the HTTP edge and
/// the aggregate rejects it again in the domain — and D5 fixes the number at 50. Every other test
/// reads the limit from <see cref="LossReasonAggregate.NameMaxLength"/>, which means all of them
/// would keep passing if someone changed the constant. This is the one place that writes the literal
/// 50, and it walks both paths with the same strings, so a divergence between the two layers or a
/// silent change of the limit fails here.
/// </summary>
public sealed class LossReasonNameLengthContractTests
{
    private const int ExpectedNameMaxLength = 50;

    private static readonly string NameAtTheLimit = new('a', ExpectedNameMaxLength);
    private static readonly string NameOverTheLimit = new('a', ExpectedNameMaxLength + 1);

    [Fact]
    public void NameMaxLength_IsFiftyCharacters()
    {
        LossReasonAggregate.NameMaxLength.ShouldBe(ExpectedNameMaxLength);
    }

    [Fact]
    public void NameOfFiftyCharacters_IsAcceptedByBothTheValidatorsAndTheAggregate()
    {
        new CreateLossReasonInputValidator()
            .Validate(new CreateLossReasonInputDto(NameAtTheLimit, IsActive: true)).IsValid.ShouldBeTrue();
        new UpdateLossReasonInputValidator()
            .Validate(new UpdateLossReasonInputDto(NameAtTheLimit, IsActive: true)).IsValid.ShouldBeTrue();
        // The listing filter is no longer tied to the domain error, but it is still bounded by the
        // same number: a search longer than the longest possible name can never match a row.
        new GetLossReasonsInputValidator()
            .Validate(new GetLossReasonsInputDto(NameAtTheLimit, IsActive: null)).IsValid.ShouldBeTrue();

        LossReasonAggregate.Create(new CreateLossReasonArgs(NameAtTheLimit, IsActive: true))
            .IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void NameOfFiftyOneCharacters_IsRejectedByBothTheValidatorsAndTheAggregate()
    {
        new CreateLossReasonInputValidator()
            .Validate(new CreateLossReasonInputDto(NameOverTheLimit, IsActive: true)).IsValid.ShouldBeFalse();
        new UpdateLossReasonInputValidator()
            .Validate(new UpdateLossReasonInputDto(NameOverTheLimit, IsActive: true)).IsValid.ShouldBeFalse();
        new GetLossReasonsInputValidator()
            .Validate(new GetLossReasonsInputDto(NameOverTheLimit, IsActive: null)).IsValid.ShouldBeFalse();

        LossReasonAggregate.Create(new CreateLossReasonArgs(NameOverTheLimit, IsActive: true))
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void EmptyName_IsRejectedByTheWriteValidatorsAndTheAggregate()
    {
        new CreateLossReasonInputValidator()
            .Validate(new CreateLossReasonInputDto("", IsActive: true)).IsValid.ShouldBeFalse();
        new UpdateLossReasonInputValidator()
            .Validate(new UpdateLossReasonInputDto("", IsActive: true)).IsValid.ShouldBeFalse();

        LossReasonAggregate.Create(new CreateLossReasonArgs("", IsActive: true))
            .IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void NameOverTheLimit_IsRejectedByTheAggregateOnUpdateToo()
    {
        // The update path has its own invariant: reading a legacy row whose name is longer than the
        // limit works, but writing it back does not (R7).
        var aggregate = LossReasonAggregate.Reconstruct(1, NameAtTheLimit, isActive: true);

        aggregate.Update(new UpdateLossReasonArgs(NameOverTheLimit, IsActive: true))
            .IsFailure.ShouldBeTrue();
    }
}
