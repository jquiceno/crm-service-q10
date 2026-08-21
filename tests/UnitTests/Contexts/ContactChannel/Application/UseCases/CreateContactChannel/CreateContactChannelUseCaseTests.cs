using ContactChannel.Application.UseCases.CreateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using NSubstitute;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Application.UseCases.CreateContactChannel;

public sealed class CreateContactChannelUseCaseTests
{
    private readonly IContactChannelRepository _repository = Substitute.For<IContactChannelRepository>();

    private CreateContactChannelUseCase CreateUseCase() => new(_repository);

    private void PersistsAs(int id, string name, bool isActive) =>
        _repository
            .CreateAsync(Arg.Any<ContactChannelAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<ContactChannelAggregate>.Success(
                ContactChannelAggregate.Reconstruct(id, name, isActive)));

    [Fact]
    public async Task ExecuteAsync_WithAValidName_ReturnsTheIdentifierTheDatabaseGenerated()
    {
        PersistsAs(id: 42, name: "WhatsApp", isActive: true);

        var result = await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("WhatsApp", IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(42);
        result.Value.Name.ShouldBe("WhatsApp");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_PersistsTheAggregateTheDomainBuilt()
    {
        PersistsAs(id: 1, name: "WhatsApp", isActive: false);

        await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("  WhatsApp  ", IsActive: false));

        await _repository.Received(1).CreateAsync(
            Arg.Is<ContactChannelAggregate>(a => a.Name == "WhatsApp" && !a.IsActive),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnInvalidName_DoesNotTouchTheRepository()
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("   ", IsActive: true));

        result.IsFailure.ShouldBeTrue();
        await _repository.DidNotReceive().CreateAsync(
            Arg.Any<ContactChannelAggregate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_SealsTheDomainErrorWithTheContextAndItsOrigin()
    {
        var result = await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto(null, IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Context.ShouldBe(ContactChannelErrors.Context);
        result.Error.Origin.ShouldBe(nameof(CreateContactChannelUseCase));
        result.Error.Details.ShouldHaveSingleItem().Property.ShouldBe(nameof(ContactChannelAggregate.Name));
    }

    [Fact]
    public async Task ExecuteAsync_WhenPersistenceFails_PropagatesTheErrorWithoutResealingIt()
    {
        var persistenceError = new DomainError("boom", ErrorType.Internal) { Origin = "ContactChannelRepository" };
        _repository
            .CreateAsync(Arg.Any<ContactChannelAggregate>(), Arg.Any<CancellationToken>())
            .Returns(Result<ContactChannelAggregate>.Failure(persistenceError));

        var result = await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("WhatsApp", IsActive: true));

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe("ContactChannelRepository");
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsANameThatAlreadyExists()
    {
        PersistsAs(id: 8, name: "WhatsApp", isActive: true);

        var result = await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("WhatsApp", IsActive: true));

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).CreateAsync(
            Arg.Any<ContactChannelAggregate>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ForwardsTheCancellationToken()
    {
        PersistsAs(id: 1, name: "WhatsApp", isActive: true);
        using var cts = new CancellationTokenSource();

        await CreateUseCase().ExecuteAsync(new CreateContactChannelInputDto("WhatsApp", IsActive: true), cts.Token);

        await _repository.Received(1).CreateAsync(Arg.Any<ContactChannelAggregate>(), cts.Token);
    }
}
