using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Domain;

public sealed class ContactChannelAggregateTests
{
    private const string ValidName = "WhatsApp";

    [Fact]
    public void Create_WithValidArguments_ReturnsSuccess()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(ValidName, IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(ValidName);
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithInactiveState_KeepsStateFalse()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(ValidName, IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Create_DoesNotAssignIdentifier_BecauseDatabaseGeneratesIt()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(ValidName, IsActive: true));

        result.Value.Id.ShouldBe(0);
    }

    [Fact]
    public void Create_WithSurroundingWhitespace_TrimsName()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs("   WhatsApp   ", IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe(ValidName);
    }

    [Fact]
    public void Create_WithNullName_ReturnsNameRequired()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(null, IsActive: true));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameRequired.Message);
    }

    [Fact]
    public void Create_WithEmptyName_ReturnsNameRequired()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(string.Empty, IsActive: true));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameRequired.Message);
    }

    [Fact]
    public void Create_WithWhitespaceOnlyName_ReturnsNameRequired()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs("     ", IsActive: true));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameRequired.Message);
    }

    [Fact]
    public void Create_WithNameAtMaxLength_ReturnsSuccess()
    {
        var name = new string('a', ContactChannelAggregate.NameMaxLength);

        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(name, IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.Length.ShouldBe(ContactChannelAggregate.NameMaxLength);
    }

    [Fact]
    public void Create_WithNameOverMaxLength_ReturnsNameTooLong()
    {
        var name = new string('a', ContactChannelAggregate.NameMaxLength + 1);

        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(name, IsActive: true));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameTooLong.Message);
    }

    [Fact]
    public void Create_WithWhitespacePaddedNameAtMaxLength_TrimsBeforeValidating()
    {
        var name = "  " + new string('a', ContactChannelAggregate.NameMaxLength) + "  ";

        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(name, IsActive: true));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Create_NeverThrows_OnInvalidInput()
    {
        Should.NotThrow(() => ContactChannelAggregate.Create(new CreateContactChannelArgs(null, IsActive: true)));
    }

    [Fact]
    public void Create_DoesNotSetAuditDates_BecauseTheCatalogIsNotAudited()
    {
        var result = ContactChannelAggregate.Create(new CreateContactChannelArgs(ValidName, IsActive: true));

        result.Value.CreatedAt.ShouldBeNull();
        result.Value.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Reconstruct_AssignsStateWithoutValidating()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: string.Empty, isActive: false);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBeEmpty();
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Reconstruct_DoesNotSetAuditDates()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        aggregate.CreatedAt.ShouldBeNull();
        aggregate.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Update_WithValidArguments_ChangesNameAndState()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        var result = aggregate.Update(new UpdateContactChannelArgs("Feria", IsActive: false));

        result.IsSuccess.ShouldBeTrue();
        aggregate.Name.ShouldBe("Feria");
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Update_WithSurroundingWhitespace_TrimsName()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        aggregate.Update(new UpdateContactChannelArgs("  Feria  ", IsActive: true));

        aggregate.Name.ShouldBe("Feria");
    }

    [Fact]
    public void Update_WithInvalidName_ReturnsNameRequiredAndLeavesAggregateUntouched()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        var result = aggregate.Update(new UpdateContactChannelArgs("   ", IsActive: false));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameRequired.Message);
        aggregate.Name.ShouldBe(ValidName);
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Update_WithNameOverMaxLength_ReturnsNameTooLong()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        var result = aggregate.Update(
            new UpdateContactChannelArgs(new string('a', ContactChannelAggregate.NameMaxLength + 1), IsActive: true));

        ShouldBeValidationFailure(result.IsFailure, result.Error, ContactChannelErrors.NameTooLong.Message);
    }

    [Fact]
    public void Update_DoesNotSetUpdatedAt_BecauseTheCatalogIsNotAudited()
    {
        var aggregate = ContactChannelAggregate.Reconstruct(id: 7, name: ValidName, isActive: true);

        aggregate.Update(new UpdateContactChannelArgs("Feria", IsActive: true));

        aggregate.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public void Equality_IsByIdentifier_NotByValue()
    {
        var one = ContactChannelAggregate.Reconstruct(id: 7, name: "Llamada", isActive: true);
        var other = ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false);

        one.ShouldBe(other);
    }

    private static void ShouldBeValidationFailure(bool isFailure, DomainError error, string expectedMessage)
    {
        isFailure.ShouldBeTrue();
        error.Type.ShouldBe(ErrorType.DomainError);

        var detail = error.Details.ShouldHaveSingleItem();
        detail.Property.ShouldBe(ContactChannelErrors.NameProperty);
        detail.Errors.ShouldNotBeNull();
        detail.Errors.ShouldContain(expectedMessage);
    }
}
