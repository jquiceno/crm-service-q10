using BusinessStatus.Application.UseCases.CreateBusinessStatus;
using BusinessStatus.Domain.Aggregates;
using BusinessStatus.Domain.Errors;
using BusinessStatus.Domain.Repositories;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.BusinessStatus.Application;

public sealed class CreateBusinessStatusUseCaseTests
{
    private const string RepositoryOrigin = "BusinessStatusRepository";

    private readonly IBusinessStatusRepository _repository = Substitute.For<IBusinessStatusRepository>();
    private readonly CreateBusinessStatusUseCase _sut;

    public CreateBusinessStatusUseCaseTests() => _sut = new CreateBusinessStatusUseCase(_repository);

    private static CreateBusinessStatusInputDto Input(
        string? name = "Negotiation",
        decimal percentage = 50m,
        string? color = "49ff7c",
        bool isActive = true) =>
        new(name, percentage, color, isActive);

    private void RepositoryReturnsPersisted(
        int id = 7, string name = "Negotiation", int? percentage = 50, string? color = "49ff7c", bool isActive = true) =>
        _repository
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Success(
                BusinessStatusAggregate.Reconstruct(id, name, percentage, color, isActive)));

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsTheAssignedIdentity()
    {
        RepositoryReturnsPersisted();

        var result = await _sut.ExecuteAsync(Input());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("Negotiation");
        result.Value.Percentage.ShouldBe(50);
        result.Value.Color.ShouldBe("49ff7c");
        result.Value.IsActive.ShouldBeTrue();
        await _repository.Received(1)
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutColor_ReturnsNullColor()
    {
        RepositoryReturnsPersisted(color: null);

        var result = await _sut.ExecuteAsync(Input(color: null));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Color.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheAggregateBuiltFromTheInput()
    {
        RepositoryReturnsPersisted();

        await _sut.ExecuteAsync(Input(name: "  Negotiation  ", percentage: 30m));

        await _repository.Received(1).CreateAsync(
            Arg.Is<BusinessStatusAggregate>(a => a.Name == "Negotiation" && a.Percentage == 30 && a.Id == 0),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task ExecuteAsync_WithTerminalPercentage_FailsWithoutTouchingTheRepository(int percentage)
    {
        var result = await _sut.ExecuteAsync(Input(percentage: percentage));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.DomainError);
        result.Error.Details.ShouldContain(detail => detail.Property == "Percentage");
        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithEmptyNameAndTerminalPercentage_ReturnsBothErrors()
    {
        var result = await _sut.ExecuteAsync(Input(name: "   ", percentage: 0m));

        result.IsFailure.ShouldBeTrue();
        var properties = result.Error.Details.Select(detail => detail.Property).ToList();
        properties.Count.ShouldBe(2);
        properties.ShouldContain("Name");
        properties.ShouldContain("Percentage");
        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithNonIntegerPercentage_FailsWithoutTouchingTheRepository()
    {
        var result = await _sut.ExecuteAsync(Input(percentage: 50.5m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail => detail.Property == "Percentage");
        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidColor_FailsWithoutTouchingTheRepository()
    {
        var result = await _sut.ExecuteAsync(Input(color: "zzzzzz"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldContain(detail => detail.Property == "Color");
        await _repository.DidNotReceive()
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheDomainFails_SealsTheErrorWithContextAndOrigin()
    {
        var result = await _sut.ExecuteAsync(Input(percentage: 100m));

        result.Error.Context.ShouldBe(BusinessStatusErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateBusinessStatusUseCase));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryFails_PropagatesTheErrorUntouched()
    {
        var failure = new DomainError("boom", ErrorType.Internal)
        {
            Context = BusinessStatusErrors.Context,
            Origin = RepositoryOrigin
        };
        _repository
            .CreateAsync(Arg.Any<BusinessStatusAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<BusinessStatusAggregate>.Failure(failure));

        var result = await _sut.ExecuteAsync(Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(RepositoryOrigin, "the use case does not replace the origin of the failure");
    }
}
