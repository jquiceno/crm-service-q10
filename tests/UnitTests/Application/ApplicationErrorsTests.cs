using Shared.Application;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Application;

public sealed class ApplicationErrorsTests
{
    [Fact]
    public void ValidationFailed_BuildsValidationErrorWithContextOriginAndDetails()
    {
        ValidationError[] errors = [new("Required.", ErrorType.Validation) { Property = "name" }];

        var error = ApplicationErrors.ValidationFailed(errors, "CreateAnnouncement", "AnnouncementValidator");

        error.Message.ShouldBe("Validation failed");
        error.Type.ShouldBe(ErrorType.Validation);
        error.Context.ShouldBe("CreateAnnouncement");
        error.Origin.ShouldBe("AnnouncementValidator");
        error.Details.Count.ShouldBe(1);
        error.Details[0].Property.ShouldBe("name");
    }

    [Fact]
    public void ValidationFailed_WithNoErrors_ReturnsEmptyDetails()
    {
        var error = ApplicationErrors.ValidationFailed([], "Context", "Origin");

        error.Details.ShouldBeEmpty();
    }
}
