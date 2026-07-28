using Shared.Presentation.Mapping;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Shared.Presentation;

public sealed class ErrorHttpMapperTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    [InlineData(ErrorType.Unauthorized, 401)]
    [InlineData(ErrorType.Forbidden, 403)]
    [InlineData(ErrorType.Internal, 500)]
    [InlineData(ErrorType.DomainError, 400)]
    [InlineData(ErrorType.None, 500)]
    public void ToHttpStatusCode_MapsErrorTypeToExpectedStatusCode(ErrorType errorType, int expectedStatusCode)
    {
        var result = ErrorHttpMapper.ToHttpStatusCode(errorType);

        ((int)result).ShouldBe(expectedStatusCode);
    }

    [Theory]
    [InlineData(ErrorType.None, "NONE")]
    [InlineData(ErrorType.Validation, "VALIDATION")]
    [InlineData(ErrorType.NotFound, "NOT_FOUND")]
    [InlineData(ErrorType.Conflict, "CONFLICT")]
    [InlineData(ErrorType.Unauthorized, "UNAUTHORIZED")]
    [InlineData(ErrorType.Forbidden, "FORBIDDEN")]
    [InlineData(ErrorType.Internal, "INTERNAL")]
    [InlineData(ErrorType.DomainError, "DOMAIN_VALIDATION")]
    public void ToErrorTypeName_MapsKnownErrorTypes(ErrorType errorType, string expectedName)
    {
        var result = ErrorHttpMapper.ToErrorTypeName(errorType);

        result.ShouldBe(expectedName);
    }

    [Fact]
    public void ToErrorTypeName_WhenErrorTypeIsUnmapped_ReturnsInternalFallback()
    {
        var result = ErrorHttpMapper.ToErrorTypeName((ErrorType)999);

        result.ShouldBe("INTERNAL");
    }

    [Fact]
    public void ToErrorCode_CombinesPrefixWithErrorTypeName()
    {
        var result = ErrorHttpMapper.ToErrorCode(ErrorType.NotFound);

        result.ShouldBe("HTTP.NOT_FOUND");
    }

    [Fact]
    public void ToErrorDetailDtos_WhenEmpty_ReturnsEmptyArray()
    {
        var result = ErrorHttpMapper.ToErrorDetailDtos([]);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void ToErrorDetailDtos_WhenDetailHasNoChildren_MapsFlatDetail()
    {
        var detail = new ErrorDetail("Name", ["Required"]);

        var result = ErrorHttpMapper.ToErrorDetailDtos([detail]);

        result.Length.ShouldBe(1);
        result[0].Property.ShouldBe("name");
        result[0].Errors.ShouldBe(["Required"]);
        result[0].Children.ShouldBeNull();
    }

    [Fact]
    public void ToErrorDetailDtos_WhenDetailHasChildren_MapsRecursively()
    {
        var child = new ErrorDetail("City", ["Required"]);
        var parent = new ErrorDetail("Address", null, null, null, [child]);

        var result = ErrorHttpMapper.ToErrorDetailDtos([parent]);

        result.Length.ShouldBe(1);
        result[0].Property.ShouldBe("address");
        result[0].Children.ShouldNotBeNull();
        result[0].Children![0].Property.ShouldBe("city");
        result[0].Children![0].Errors.ShouldBe(["Required"]);
    }

    [Fact]
    public void ToErrorDetailDtos_WhenPropertyIsEmpty_ReturnsEmptyPropertyUnchanged()
    {
        var detail = new ErrorDetail(string.Empty, ["Required"]);

        var result = ErrorHttpMapper.ToErrorDetailDtos([detail]);

        result[0].Property.ShouldBe(string.Empty);
    }
}
