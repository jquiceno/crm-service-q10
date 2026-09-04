using System.Globalization;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Domain;

public sealed class ContactChannelErrorsTests
{
    [Fact]
    public void NameRequired_IsAValidationErrorCarryingItsProperty()
    {
        ContactChannelErrors.NameRequired.Type.ShouldBe(ErrorType.Validation);
        ContactChannelErrors.NameRequired.Property.ShouldBe(nameof(ContactChannelAggregate.Name));
    }

    [Fact]
    public void NameTooLong_IsAValidationErrorCarryingItsProperty()
    {
        ContactChannelErrors.NameTooLong.Type.ShouldBe(ErrorType.Validation);
        ContactChannelErrors.NameTooLong.Property.ShouldBe(nameof(ContactChannelAggregate.Name));
    }

    [Fact]
    public void NameTooLong_PublishesTheMaximumLengthAsAnAttribute()
    {
        var attributes = ContactChannelErrors.NameTooLong.Attributes;

        attributes.ShouldNotBeNull();
        attributes["maxLength"].ShouldBe(ContactChannelAggregate.NameMaxLength);
    }

    [Fact]
    public void NameTooLong_NamesTheLimitInItsMessage()
    {
        ContactChannelErrors.NameTooLong.Message.ShouldContain(
            ContactChannelAggregate.NameMaxLength.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TheValidationErrors_CarryNoContext_BecauseTheUseCaseStampsIt()
    {
        ContactChannelErrors.NameRequired.Context.ShouldBeEmpty();
        ContactChannelErrors.NameTooLong.Context.ShouldBeEmpty();
    }

    [Fact]
    public void NotFound_IsANotFoundErrorCarryingTheIdentifier()
    {
        var error = ContactChannelErrors.NotFound(42);

        error.Type.ShouldBe(ErrorType.NotFound);
        error.Context.ShouldBe(ContactChannelErrors.Context);
        error.Message.ShouldContain("42");
    }

    [Fact]
    public void InUse_IsAConflictErrorCarryingTheIdentifier()
    {
        var error = ContactChannelErrors.InUse(42);

        error.Type.ShouldBe(ErrorType.Conflict);
        error.Context.ShouldBe(ContactChannelErrors.Context);
        error.Message.ShouldContain("42");
    }
}
