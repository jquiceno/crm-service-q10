using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Results;

public sealed class ResultTests
{
    [Fact]
    public void Success_ReturnsSuccessfulResultWithNoError()
    {
        var result = Result.Success();

        result.IsSuccess.ShouldBeTrue();
        result.IsFailure.ShouldBeFalse();
    }

    [Fact]
    public void Failure_ReturnsFailedResultWithGivenError()
    {
        var error = new DomainError("Something went wrong.", ErrorType.Internal);

        var result = Result.Failure(error);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public void Error_OnSuccessfulResult_Throws()
    {
        var result = Result.Success();

        Should.Throw<InvalidOperationException>(() => result.Error);
    }

    [Fact]
    public void ImplicitOperator_FromDomainError_ReturnsFailure()
    {
        Result result = new DomainError("boom", ErrorType.Internal);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("boom");
    }

    [Fact]
    public void GenericSuccess_ReturnsValueAndSuccessState()
    {
        var result = Result<int>.Success(42);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericFailure_ValueAccess_Throws()
    {
        var result = Result<int>.Failure(new DomainError("boom", ErrorType.Internal));

        result.IsFailure.ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void GenericImplicitOperator_FromValue_ReturnsSuccess()
    {
        Result<int> result = 42;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(42);
    }

    [Fact]
    public void GenericImplicitOperator_FromDomainError_ReturnsFailure()
    {
        Result<int> result = new DomainError("boom", ErrorType.Internal);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("boom");
    }

    [Fact]
    public void TypedSuccess_ReturnsValueAndSuccessState()
    {
        var result = Result<int, ValidationError>.Success(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(7);
    }

    [Fact]
    public void TypedFailure_ExposesTypedError()
    {
        var typedError = new ValidationError("Required.", ErrorType.Validation) { Property = "name" };

        var result = Result<int, ValidationError>.Failure(typedError);

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(typedError);
        result.TypedError.Property.ShouldBe("name");
    }

    [Fact]
    public void TypedError_OnSuccessfulResult_Throws()
    {
        var result = Result<int, ValidationError>.Success(1);

        Should.Throw<InvalidOperationException>(() => result.TypedError);
    }

    [Fact]
    public void TypedImplicitOperator_FromValue_ReturnsSuccess()
    {
        Result<int, ValidationError> result = 5;

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(5);
    }

    [Fact]
    public void TypedImplicitOperator_FromTypedError_ReturnsFailure()
    {
        var typedError = new ValidationError("Required.", ErrorType.Validation) { Property = "name" };

        Result<int, ValidationError> result = typedError;

        result.IsFailure.ShouldBeTrue();
        result.TypedError.ShouldBe(typedError);
    }
}
