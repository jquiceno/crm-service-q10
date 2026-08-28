using System.Diagnostics.CodeAnalysis;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Results;

public sealed class ErrorsTests
{
    [Fact]
    public void DomainError_Equals_IgnoresContextOriginAndDetails()
    {
        var left = new DomainError("Same message", ErrorType.NotFound) { Context = "A", Origin = "X" };
        var right = new DomainError("Same message", ErrorType.NotFound) { Context = "B", Origin = "Y" };

        left.Equals(right).ShouldBeTrue();
        left.GetHashCode().ShouldBe(right.GetHashCode());
    }

    [Fact]
    public void DomainError_Equals_WithDifferentMessage_ReturnsFalse()
    {
        var left = new DomainError("A", ErrorType.NotFound);
        var right = new DomainError("B", ErrorType.NotFound);

        left.Equals(right).ShouldBeFalse();
    }

    [Fact]
    [SuppressMessage("Maintainability", "CA1508:Avoid dead conditional code", Justification = "Equality-contract test: the constant outcome is the behavior under test.")]
    public void DomainError_Equals_WithNull_ReturnsFalse()
    {
        var error = new DomainError("A", ErrorType.NotFound);

        error.Equals(null).ShouldBeFalse();
    }

    [Fact]
    public void DomainError_FromValidationDomainErrors_WithoutChildren_BuildsFlatDetails()
    {
        ValidationError[] errors =
        [
            new("Required.", ErrorType.Validation) { Property = "name" },
            new("Too long.", ErrorType.Validation) { Property = "name" },
        ];

        var error = DomainError.FromValidationDomainErrors(errors);

        error.Type.ShouldBe(ErrorType.DomainError);
        error.Details.Count.ShouldBe(1);
        error.Details[0].Property.ShouldBe("name");
        error.Details[0].Errors.ShouldBe(["Required.", "Too long."]);
        error.Details[0].Children.ShouldBeNull();
    }

    [Fact]
    public void DomainError_FromValidationDomainErrors_WithNestedChildren_BuildsHierarchicalDetails()
    {
        var childError = new ValidationError("Child invalid.", ErrorType.Validation) { Property = "childProp" };
        var parentError = new ValidationError("Parent invalid.", ErrorType.Validation)
        {
            Property = "parent",
            Children = [childError],
        };

        var error = DomainError.FromValidationDomainErrors([parentError]);

        error.Details.Count.ShouldBe(1);
        error.Details[0].Property.ShouldBe("parent");
        error.Details[0].Children.ShouldNotBeNull();
        error.Details[0].Children![0].Property.ShouldBe("childProp");
    }

    [Fact]
    public void SharedErrors_NotFound_BuildsMessageWithEntityNameAndId()
    {
        var error = SharedErrors.NotFound("Announcement", 42);

        error.Type.ShouldBe(ErrorType.NotFound);
        error.Message.ShouldBe("Announcement with id '42' was not found.");
    }

    [Fact]
    public void PersistenceValidationError_Constructor_SetsValidationType()
    {
        var error = new PersistenceValidationError("Duplicate key.");

        error.Type.ShouldBe(ErrorType.Validation);
        error.Message.ShouldBe("Duplicate key.");
    }

    [Fact]
    public void ConflictError_Constructor_SetsConflictType()
    {
        var error = new ConflictError("Already exists.");

        error.Type.ShouldBe(ErrorType.Conflict);
        error.Message.ShouldBe("Already exists.");
    }
}
