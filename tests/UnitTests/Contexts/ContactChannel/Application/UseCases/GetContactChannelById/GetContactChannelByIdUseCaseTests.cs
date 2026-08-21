using ContactChannel.Application.UseCases.GetContactChannelById;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Application.UseCases.GetContactChannelById;

public sealed class GetContactChannelByIdUseCaseTests
{
    private readonly IContactChannelRepository _repository = Substitute.For<IContactChannelRepository>();

    private GetContactChannelByIdUseCase CreateUseCase() => new(_repository);

    private void Returns(Result<ContactChannelAggregate> result) =>
        _repository.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(result);

    [Fact]
    public async Task ExecuteAsync_WhenTheChannelExists_MapsItToTheOutputDto()
    {
        Returns(ContactChannelAggregate.Reconstruct(id: 7, name: "WhatsApp", isActive: true));

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(new GetContactChannelByIdOutputDto(7, "WhatsApp", true));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheChannelIsInactive_KeepsTheStateFalse()
    {
        Returns(ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false));

        var result = await CreateUseCase().ExecuteAsync(7);

        result.Value.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheIdentifierAndTheCancellationToken()
    {
        Returns(ContactChannelAggregate.Reconstruct(id: 7, name: "WhatsApp", isActive: true));
        using var cancellation = new CancellationTokenSource();

        await CreateUseCase().ExecuteAsync(7, cancellation.Token);

        await _repository.Received(1).GetByIdAsync(7, cancellation.Token);
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_PropagatesTheNotFoundUntouched()
    {
        var notFound = ContactChannelErrors.NotFound(404) with { Origin = "ContactChannelRepository" };
        Returns(Result<ContactChannelAggregate>.Failure(notFound));

        var result = await CreateUseCase().ExecuteAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("404");
        result.Error.Origin.ShouldBe(
            "ContactChannelRepository",
            "the use case does not replace the origin of a failure it did not produce");
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePersistenceFails_PropagatesTheErrorUntouched()
    {
        var failure = new InternalError("A persistence error occurred.") { Origin = "ContactChannelRepository" };
        Returns(Result<ContactChannelAggregate>.Failure(failure));

        var result = await CreateUseCase().ExecuteAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe("ContactChannelRepository");
        result.Error.Context.ShouldBeEmpty(
            "the use case does not stamp its own context on a failure it did not produce");
    }
}
