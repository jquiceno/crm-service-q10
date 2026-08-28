using ContactChannel.Application.UseCases.UpdateContactChannel;
using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Errors;
using ContactChannel.Domain.Repositories;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Application.UseCases.UpdateContactChannel;

public sealed class UpdateContactChannelUseCaseTests
{
    private readonly IContactChannelRepository _repository = Substitute.For<IContactChannelRepository>();
    private readonly IUnitOfWorkPort _unitOfWork = Substitute.For<IUnitOfWorkPort>();

    private UpdateContactChannelUseCase CreateUseCase() => new(_repository, _unitOfWork);

    private ContactChannelAggregate Existing(int id = 7, string name = "WhatsApp", bool isActive = true)
    {
        var channel = ContactChannelAggregate.Reconstruct(id, name, isActive);
        _repository.GetByIdAsync(id, Arg.Any<CancellationToken>())
            .Returns(Result<ContactChannelAggregate>.Success(channel));
        _repository.Update(Arg.Any<ContactChannelAggregate>()).Returns(Result.Success());
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
        return channel;
    }

    private static UpdateContactChannelInputDto Input(string? name = "Feria", bool? isActive = false) =>
        new(name, isActive);

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ReturnsTheIdentifierAndAppliesTheChanges()
    {
        var channel = Existing();

        var result = await CreateUseCase().ExecuteAsync(7, Input());

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        channel.Name.ShouldBe("Feria");
        channel.IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_PersistsAndCommitsOnce()
    {
        var channel = Existing();

        await CreateUseCase().ExecuteAsync(7, Input());

        _repository.Received(1).Update(channel);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_TrimsTheNameThroughTheAggregate()
    {
        var channel = Existing();

        await CreateUseCase().ExecuteAsync(7, Input("  Feria  "));

        channel.Name.ShouldBe("Feria");
    }

    [Fact]
    public async Task ExecuteAsync_WithAnUnknownId_PropagatesNotFoundWithoutWriting()
    {
        _repository.GetByIdAsync(7, Arg.Any<CancellationToken>())
            .Returns(Result<ContactChannelAggregate>.Failure(ContactChannelErrors.NotFound(7)));

        var result = await CreateUseCase().ExecuteAsync(7, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        _repository.DidNotReceive().Update(Arg.Any<ContactChannelAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithAnInvalidName_DoesNotPersistAndSealsTheDomainError()
    {
        Existing();

        var result = await CreateUseCase().ExecuteAsync(7, Input("   "));

        result.IsFailure.ShouldBeTrue();
        result.Error.Context.ShouldBe(ContactChannelErrors.Context);
        result.Error.Origin.ShouldBe(nameof(UpdateContactChannelUseCase));
        _repository.DidNotReceive().Update(Arg.Any<ContactChannelAggregate>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithoutState_DoesNotPersist()
    {
        Existing();

        var result = await CreateUseCase().ExecuteAsync(7, Input(isActive: null));

        result.IsFailure.ShouldBeTrue();
        result.Error.Details.ShouldHaveSingleItem().Property.ShouldBe(
            nameof(ContactChannelAggregate.IsActive));
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheRepositoryUpdateFails_PropagatesItsOriginWithoutCommitting()
    {
        Existing();
        _repository.Update(Arg.Any<ContactChannelAggregate>())
            .Returns(new InternalError("A persistence error occurred.") { Origin = "ContactChannelRepository" });

        var result = await CreateUseCase().ExecuteAsync(7, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(
            "ContactChannelRepository",
            "the use case does not replace the origin of the failure");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheCommitFails_PropagatesTheErrorWithoutResealingIt()
    {
        Existing();
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(Result.Failure(new DomainError("boom", ErrorType.Internal) { Origin = "UnitOfWorkAdapter" }));

        var result = await CreateUseCase().ExecuteAsync(7, Input());

        result.IsFailure.ShouldBeTrue();
        result.Error.Origin.ShouldBe("UnitOfWorkAdapter");
    }

    [Fact]
    public async Task ExecuteAsync_AcceptsANameThatAlreadyExists()
    {
        Existing(name: "Feria");

        var result = await CreateUseCase().ExecuteAsync(7, Input("Feria"));

        result.IsSuccess.ShouldBeTrue();
    }
}
