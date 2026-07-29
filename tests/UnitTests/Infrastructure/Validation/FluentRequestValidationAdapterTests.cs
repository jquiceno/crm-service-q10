using FluentValidation;
using FluentValidation.Results;
using Infrastructure.Adapters.Validation;
using Infrastructure.Validation.FluentValidation;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Validation;

public sealed class FluentRequestValidationAdapterTests
{
    public sealed record ItemDto(string? Name);

    public sealed record AddressDto(string? City, string? Zip);

    public sealed record SampleDto(string? Name, AddressDto? Address, IEnumerable<ItemDto>? Items);

    private sealed class ItemValidator : AbstractValidator<ItemDto>
    {
        public ItemValidator() =>
            RuleFor(x => x.Name).NotEmpty().WithMessage("Item name is required.");
    }

    private sealed class SampleValidator : AbstractValidator<SampleDto>, IStructuralValidator<SampleDto>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("Name is required.");

            When(x => x.Address is not null, () =>
            {
                RuleFor(x => x.Address!.City)
                    .NotEmpty()
                    .WithMessage("City is required.")
                    .WithState(_ => new ValidationError("City is required.", ErrorType.DomainError)
                    {
                        Attributes = new Dictionary<string, object?> { ["reason"] = "city-missing" },
                    });
            });

            RuleFor(x => x.Address)
                .Must(a => a is null || !string.IsNullOrEmpty(a.Zip))
                .WithMessage("Address zip is missing.")
                .WithState(_ => new ValidationError("Address zip is missing.", ErrorType.Conflict)
                {
                    Attributes = new Dictionary<string, object?> { ["reason"] = "zip-missing" },
                });

            RuleForEach(x => x.Items).SetValidator(new ItemValidator());
        }
    }

    private static FluentRequestValidationAdapter<SampleDto> BuildRealSut() => new(new SampleValidator());

    private static ErrorDetail Detail(Result result, string property) =>
        result.Error.Details.Single(d => d.Property == property);

    [Fact]
    public async Task ValidateAsync_WithValidInput_ReturnsSuccess()
    {
        var sut = BuildRealSut();
        var input = new SampleDto("Announcement", new AddressDto("Springfield", "00001"), null);

        var result = await sut.ValidateAsync(input);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithFlatFailure_ReturnsSingleValidationErrorWithAttemptedValue()
    {
        var sut = BuildRealSut();
        var input = new SampleDto(string.Empty, null, null);

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Validation);
        var detail = Detail(result, "Name");
        detail.Errors.ShouldNotBeNull();
        detail.Errors!.ShouldContain("Name is required.");
        detail.Children.ShouldBeNull();
    }

    [Fact]
    public async Task ValidateAsync_WithNullAddress_SkipsConditionalChildRuleAndPassesMustRule()
    {
        var sut = BuildRealSut();
        var input = new SampleDto("Announcement", null, null);

        var result = await sut.ValidateAsync(input);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_WithOnlyNestedFailure_BuildsParentNodeWithChildAndNoAttributes()
    {
        var sut = BuildRealSut();
        // City empty (nested failure), Zip present so the flat "Must" rule passes.
        var input = new SampleDto("Announcement", new AddressDto(string.Empty, "00001"), null);

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        var parent = Detail(result, "Address");
        parent.Errors.ShouldBeNull();
        parent.Value.ShouldBeNull();
        parent.Attributes.ShouldBeNull();
        parent.Children.ShouldNotBeNull();
        var child = parent.Children!.Single(c => c.Property == "City");
        child.Errors.ShouldNotBeNull();
        child.Errors!.ShouldContain("City is required.");
        child.Attributes.ShouldNotBeNull();
        child.Attributes!["reason"].ShouldBe("city-missing");
    }

    [Fact]
    public async Task ValidateAsync_WithFlatAndNestedFailures_ParentAttributesComeFromFlatFailureState()
    {
        var sut = BuildRealSut();
        // City empty (nested) AND Zip missing (flat "Must" rule) fail together.
        var input = new SampleDto("Announcement", new AddressDto(string.Empty, null), null);

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        var parent = Detail(result, "Address");
        parent.Errors.ShouldBeNull();
        // The parent node's attributes are sourced from the flat failure's CustomState
        // (the "firstFlat" branch), which is only reachable when a direct rule also fails.
        parent.Attributes.ShouldNotBeNull();
        parent.Attributes!["reason"].ShouldBe("zip-missing");
        parent.Children.ShouldNotBeNull();
        parent.Children!.Single(c => c.Property == "City").Errors!.ShouldContain("City is required.");
    }

    [Fact]
    public async Task ValidateAsync_WithFailingCollectionItem_BuildsIndexedParentWithChild()
    {
        var sut = BuildRealSut();
        var input = new SampleDto("Announcement", null, new List<ItemDto> { new(string.Empty) });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        var parent = Detail(result, "Items[0]");
        parent.Children.ShouldNotBeNull();
        var child = parent.Children!.Single(c => c.Property == "Name");
        child.Errors!.ShouldContain("Item name is required.");
    }

    [Fact]
    public async Task ValidateAsync_WithFailingItemInNonListEnumerable_ResolvesChildSourceViaEnumerableFallback()
    {
        var sut = BuildRealSut();
        // A HashSet<T> is IEnumerable<T> but not IList — exercises GetPropertyValue's
        // IEnumerable fallback branch (as opposed to the IList fast path).
        var input = new SampleDto("Announcement", null, new HashSet<ItemDto> { new(string.Empty) });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        var parent = Detail(result, "Items[0]");
        parent.Children!.Single(c => c.Property == "Name").Errors!.ShouldContain("Item name is required.");
    }

    [Fact]
    public async Task ValidateAsync_WithMultipleFailingItems_BuildsOneParentNodePerIndex()
    {
        var sut = BuildRealSut();
        var input = new SampleDto(
            "Announcement",
            null,
            new List<ItemDto> { new(string.Empty), new(string.Empty), new("valid") });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.Count(d => d.Property is "Items[0]" or "Items[1]").ShouldBe(2);
    }

    [Fact]
    public async Task ValidateAsync_ViaNonGenericPort_DelegatesToGenericValidation()
    {
        var sut = BuildRealSut();
        IRequestValidatorPort nonGeneric = sut;
        object input = new SampleDto("Announcement", null, null);

        var result = await nonGeneric.ValidateAsync(input, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateAsync_ViaNonGenericPort_WithInvalidInput_ReturnsFailure()
    {
        var sut = BuildRealSut();
        IRequestValidatorPort nonGeneric = sut;
        object input = new SampleDto(string.Empty, null, null);

        var result = await nonGeneric.ValidateAsync(input, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
    }

    // --- Edge cases for GetPropertyValue's reflection/bracket navigation that cannot be
    // produced by FluentValidation's own rule engine (it never emits out-of-range indices
    // or malformed paths), so a crafted ValidationResult is used instead. ---

    private static FluentRequestValidationAdapter<SampleDto> BuildMockedSut(params ValidationFailure[] failures)
    {
        var validator = Substitute.For<IStructuralValidator<SampleDto>>();
        validator.ValidateAsync(Arg.Any<SampleDto>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ValidationResult(failures)));
        return new FluentRequestValidationAdapter<SampleDto>(validator);
    }

    [Fact]
    public async Task ValidateAsync_WithOutOfRangeIndex_DoesNotThrowAndStillBuildsError()
    {
        var sut = BuildMockedSut(new ValidationFailure("Items[5].Name", "Item name is required."));
        var input = new SampleDto("x", null, new List<ItemDto> { new("only-one") });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        Detail(result, "Items[5]").Children!.Single().Property.ShouldBe("Name");
    }

    [Fact]
    public async Task ValidateAsync_WithMalformedBracketMissingClosingBracket_TreatsSegmentAsOpaqueKey()
    {
        var sut = BuildMockedSut(new ValidationFailure("Items[0.Name", "msg"));
        var input = new SampleDto("x", null, new List<ItemDto> { new("only-one") });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        Detail(result, "Items[0").Children!.Single().Property.ShouldBe("Name");
    }

    [Fact]
    public async Task ValidateAsync_WithNonNumericBracketIndex_ReturnsNullChildSourceGracefully()
    {
        var sut = BuildMockedSut(new ValidationFailure("Items[x].Name", "msg"));
        var input = new SampleDto("x", null, new List<ItemDto> { new("only-one") });

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        // The closing bracket is present, so the root key keeps it — only the
        // non-numeric index inside fails to parse, yielding a null child source.
        Detail(result, "Items[x]").Children!.Single().Property.ShouldBe("Name");
    }

    [Fact]
    public async Task ValidateAsync_WithUnknownRootProperty_ReturnsNullChildSourceGracefully()
    {
        var sut = BuildMockedSut(new ValidationFailure("Ghost.Foo", "msg"));
        var input = new SampleDto("x", null, null);

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        Detail(result, "Ghost").Children!.Single().Property.ShouldBe("Foo");
    }

    [Fact]
    public async Task ValidateAsync_WithNullCollectionProperty_BracketAccessReturnsNullGracefully()
    {
        var sut = BuildMockedSut(new ValidationFailure("Items[0].Name", "msg"));
        var input = new SampleDto("x", null, null);

        var result = await sut.ValidateAsync(input);

        result.IsFailure.ShouldBeTrue();
        Detail(result, "Items[0]").Children!.Single().Property.ShouldBe("Name");
    }
}
