using Microsoft.AspNetCore.Mvc.ModelBinding;
using Shared.Presentation.Filters;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class ModelStateValidationAdapterTests
{
    [Fact]
    public void Build_WhenModelStateHasNoErrors_ReturnsEmpty()
    {
        var modelState = new ModelStateDictionary();

        var result = ModelStateValidationAdapter.Build(modelState);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void Build_WithSinglePlainFieldError_ReturnsValidationErrorWithLowerCamelProperty()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Name", "Required");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("name");
        result[0].Message.ShouldBe("Required");
        result[0].Type.ShouldBe(ErrorType.Validation);
    }

    [Fact]
    public void Build_WithMultipleErrorsOnSameKey_ReturnsOneValidationErrorPerMessage()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Required");
        modelState.AddModelError("name", "Too short");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(2);
        result.ShouldAllBe(e => e.Property == "name");
        result.Select(e => e.Message).ShouldBe(["Required", "Too short"]);
    }

    [Fact]
    public void Build_WithEmptyErrorMessage_FallsBackToInvalidValue()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", string.Empty);

        var result = ModelStateValidationAdapter.Build(modelState);

        result[0].Message.ShouldBe("Invalid value.");
    }

    [Fact]
    public void Build_WithNestedPathAndNoDirectParentError_WrapsChildWithDefaultMessage()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Address.City", "Required");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("address");
        result[0].Message.ShouldBe("address is invalid.");
        result[0].Children.ShouldNotBeNull();
        result[0].Children![0].Property.ShouldBe("city");
        result[0].Children![0].Message.ShouldBe("Required");
    }

    [Fact]
    public void Build_WithNestedPathAndDirectParentError_UsesDirectMessageForWrapper()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("Address", "Address required");
        modelState.AddModelError("Address.City", "Required");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("address");
        result[0].Message.ShouldBe("Address required");
        result[0].Children![0].Property.ShouldBe("city");
    }

    [Fact]
    public void Build_WithThreeLevelNestedPath_BuildsNestedChildrenRecursively()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("A.B.C", "Deep error");

        var result = ModelStateValidationAdapter.Build(modelState);

        result[0].Property.ShouldBe("a");
        result[0].Children![0].Property.ShouldBe("b");
        result[0].Children![0].Children![0].Property.ShouldBe("c");
        result[0].Children![0].Children![0].Message.ShouldBe("Deep error");
    }

    [Fact]
    public void Build_WithJsonPathKeyDollarOnly_MapsToInputProperty()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$", "irrelevant message");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("input");
        result[0].Message.ShouldBe("Invalid JSON format.");
    }

    [Fact]
    public void Build_WithJsonPathKey_IgnoresOriginalMessageAndReturnsInvalidJsonFormat()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("$.age", "The JSON value could not be converted");

        var result = ModelStateValidationAdapter.Build(modelState);

        result[0].Property.ShouldBe("age");
        result[0].Message.ShouldBe("Invalid JSON format.");
    }

    [Fact]
    public void Build_WhenJsonPathErrorsExist_DropsPlainFieldErrors()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError("name", "Required");
        modelState.AddModelError("$.age", "bad json");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("age");
    }

    [Fact]
    public void Build_WithEmptyKeyAndNoJsonPathErrors_MapsToInputProperty()
    {
        var modelState = new ModelStateDictionary();
        modelState.AddModelError(string.Empty, "Required");

        var result = ModelStateValidationAdapter.Build(modelState);

        result.Count.ShouldBe(1);
        result[0].Property.ShouldBe("input");
        result[0].Message.ShouldBe("Required");
    }
}
