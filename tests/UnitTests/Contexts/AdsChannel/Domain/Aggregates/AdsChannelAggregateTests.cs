using AdsChannel.Domain.Aggregates;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Domain.Aggregates;

public sealed class AdsChannelAggregateTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_WithValidInput_ReturnsSuccessWithNameAndIsActiveSet()
    {
        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads", IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Google Ads");
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithoutExplicitIsActive_DefaultsToTrue()
    {
        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidInput_SetsCreatedAtAndUpdatedAt()
    {
        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Google Ads"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.CreatedAt.ShouldNotBeNull();
        result.Value.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithMissingName_ReturnsNameRequiredValidationError(string? name)
    {
        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs(name));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.Count.ShouldBe(1);
        result.Error.Details[0].Property.ShouldBe(nameof(AdsChannelAggregate.Name));
        result.Error.Details[0].Errors.ShouldNotBeNull();
        result.Error.Details[0].Errors!.ShouldContain("Name is required.");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsNameTooLongValidationError()
    {
        var name = new string('a', AdsChannelAggregate.MaxNameLength + 1);

        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs(name));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details[0].Property.ShouldBe(nameof(AdsChannelAggregate.Name));
        result.Error.Details[0].Errors.ShouldNotBeNull();
        result.Error.Details[0].Errors!.ShouldContain(
            $"Name cannot exceed {AdsChannelAggregate.MaxNameLength} characters.");
    }

    [Fact]
    public void Create_WithNameOfExactlyMaxLength_Succeeds()
    {
        var name = new string('a', AdsChannelAggregate.MaxNameLength);

        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs(name));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(name);
    }

    [Fact]
    public void Create_TrimsLeadingAndTrailingWhitespaceFromName()
    {
        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs("  Google Ads  "));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Google Ads");
    }

    [Fact]
    public void Create_WithNameExceedingMaxLengthOnlyBeforeTrimming_Succeeds()
    {
        var name = "  " + new string('a', AdsChannelAggregate.MaxNameLength) + "  ";

        var result = AdsChannelAggregate.Create(new CreateAdsChannelArgs(name));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.Length.ShouldBe(AdsChannelAggregate.MaxNameLength);
    }

    // ── Reconstruct ───────────────────────────────────────────────────────────

    [Fact]
    public void Reconstruct_WithProvidedValues_SetsIdNameAndIsActive()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(42, "Meta Ads", false);

        aggregate.Id.ShouldBe(42);
        aggregate.Name.ShouldBe("Meta Ads");
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Reconstruct_WithNullNameAndIsActive_DefaultsToEmptyStringAndTrue()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, null, null);

        aggregate.Name.ShouldBe(string.Empty);
        aggregate.IsActive.ShouldBeTrue();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_WithValidInput_UpdatesNameIsActiveAndUpdatedAt()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Old name", true);

        var result = aggregate.Update(new UpdateAdsChannelArgs("New name", false));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("New name");
        aggregate.IsActive.ShouldBeFalse();
        aggregate.UpdatedAt.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithMissingName_ReturnsNameRequiredAndDoesNotMutate(string? name)
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Old name", true);

        var result = aggregate.Update(new UpdateAdsChannelArgs(name!, false));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldBe("Name is required.");
        aggregate.Name.ShouldBe("Old name");
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Update_WithNameExceedingMaxLength_ReturnsNameTooLongAndDoesNotMutate()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Old name", true);

        var result = aggregate.Update(
            new UpdateAdsChannelArgs(new string('b', AdsChannelAggregate.MaxNameLength + 1), false));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        result.Error.Message.ShouldBe($"Name cannot exceed {AdsChannelAggregate.MaxNameLength} characters.");
        aggregate.Name.ShouldBe("Old name");
    }

    [Fact]
    public void Update_TrimsLeadingAndTrailingWhitespaceFromName()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Old name", true);

        var result = aggregate.Update(new UpdateAdsChannelArgs("  New name  ", true));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("New name");
    }
}
