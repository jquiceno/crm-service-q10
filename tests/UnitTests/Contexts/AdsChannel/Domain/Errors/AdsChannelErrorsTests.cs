using AdsChannel.Domain.Aggregates;
using AdsChannel.Domain.Errors;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Domain.Errors;

public sealed class AdsChannelErrorsTests
{
    [Fact]
    public void NotFound_BuildsMessageWithId()
    {
        var error = AdsChannelErrors.NotFound(42);

        error.Type.ShouldBe(ErrorType.NotFound);
        error.Message.ShouldBe("AdsChannel with id '42' was not found.");
    }

    [Fact]
    public void NameAlreadyExists_BuildsMessageWithName()
    {
        var error = AdsChannelErrors.NameAlreadyExists("Google Ads");

        error.Type.ShouldBe(ErrorType.Conflict);
        error.Message.ShouldBe("An AdsChannel with name 'Google Ads' already exists.");
    }

    [Fact]
    public void NameRequired_IsAValidationErrorOnTheNameProperty()
    {
        AdsChannelErrors.NameRequired.Type.ShouldBe(ErrorType.Validation);
        AdsChannelErrors.NameRequired.Property.ShouldBe("Name");
    }

    [Fact]
    public void NameTooLong_IsAValidationErrorWithMaxLengthAttribute()
    {
        AdsChannelErrors.NameTooLong.Type.ShouldBe(ErrorType.Validation);
        AdsChannelErrors.NameTooLong.Property.ShouldBe("Name");
        AdsChannelErrors.NameTooLong.Attributes.ShouldNotBeNull();
        AdsChannelErrors.NameTooLong.Attributes!["maxLength"].ShouldBe(AdsChannelAggregate.MaxNameLength);
    }
}
