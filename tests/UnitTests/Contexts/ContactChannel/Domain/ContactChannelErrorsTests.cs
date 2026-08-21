using ContactChannel.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Domain;

public sealed class ContactChannelErrorsTests
{
    [Fact]
    public void NameRequired_IsAValidationErrorStampedWithContextAndProperty()
    {
        ContactChannelErrors.NameRequired.Type.ShouldBe(ErrorType.Validation);
        ContactChannelErrors.NameRequired.Property.ShouldBe(ContactChannelErrors.NameProperty);
        ContactChannelErrors.NameRequired.Context.ShouldBe(ContactChannelErrors.Context);
    }

    [Fact]
    public void NameTooLong_IsAValidationErrorStampedWithContextAndProperty()
    {
        ContactChannelErrors.NameTooLong.Type.ShouldBe(ErrorType.Validation);
        ContactChannelErrors.NameTooLong.Property.ShouldBe(ContactChannelErrors.NameProperty);
        ContactChannelErrors.NameTooLong.Context.ShouldBe(ContactChannelErrors.Context);
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
