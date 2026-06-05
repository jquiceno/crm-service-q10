using NSubstitute;
using Xunit;
using Shared.Application.UseCases.GetTenant;
using Shared.Domain.Tenants.Aggregates;
using Shared.Domain.Tenants.Errors;
using Shared.Domain.Tenants.Ports;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;

namespace UnitTests.Application;

public sealed class GetTenantByCodeUseCaseTests
{
    private readonly ITenantRepository _repository = Substitute.For<ITenantRepository>();
    private readonly GetTenantByCodeUseCase _sut;

    public GetTenantByCodeUseCaseTests()
    {
        _sut = new GetTenantByCodeUseCase(_repository);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenCodeIsNullOrWhitespace_ReturnsCodeRequired(string? code)
    {
        var result = await _sut.ExecuteAsync(new GetTenantByCodeQuery(code!));

        result.IsFailure.ShouldBeTrue();
        var codeRequiredError = result.Error.ShouldBeOfType<ValidationError>();
        codeRequiredError.Property.ShouldBe("EntityCode");
        result.Error.ShouldBe(TenantErrors.CodeRequired);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeExceeds20Characters_ReturnsCodeTooLong()
    {
        var result = await _sut.ExecuteAsync(new GetTenantByCodeQuery(new string('X', 21)));

        result.IsFailure.ShouldBeTrue();
        var codeTooLongError = result.Error.ShouldBeOfType<ValidationError>();
        codeTooLongError.Property.ShouldBe("EntityCode");
        result.Error.ShouldBe(TenantErrors.CodeTooLong);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeIsExactly20Characters_DoesNotReturnCodeTooLong()
    {
        var code = new string('X', 20);
        _repository.GetByCodeAsync(code, Arg.Any<CancellationToken>())
            .Returns(Result<TenantAggregate>.Success(TenantAggregate.Reconstruct(code, "db", 1)));

        var result = await _sut.ExecuteAsync(new GetTenantByCodeQuery(code));

        result.IsSuccess.ShouldBeTrue();
        await _repository.Received(1).GetByCodeAsync(code, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenCodeHasSurroundingWhitespace_CallsRepositoryWithTrimmedCode()
    {
        _repository.GetByCodeAsync("TENANT01", Arg.Any<CancellationToken>())
            .Returns(Result<TenantAggregate>.Success(TenantAggregate.Reconstruct("TENANT01", "db", 1)));

        await _sut.ExecuteAsync(new GetTenantByCodeQuery("  TENANT01  "));

        await _repository.Received(1).GetByCodeAsync("TENANT01", Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().GetByCodeAsync("  TENANT01  ", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositorySucceeds_ReturnsAggregate()
    {
        var aggregate = TenantAggregate.Reconstruct("TENANT01", "db_tenant", 2);
        _repository.GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantAggregate>.Success(aggregate));

        var result = await _sut.ExecuteAsync(new GetTenantByCodeQuery("TENANT01"));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Code.ShouldBe("TENANT01");
        result.Value.Database.ShouldBe("db_tenant");
        result.Value.ServerDatabase.ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRepositoryReturnsNotFound_PropagatesError()
    {
        _repository.GetByCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<TenantAggregate>.Failure(TenantErrors.NotFound("TENANT01")));

        var result = await _sut.ExecuteAsync(new GetTenantByCodeQuery("TENANT01"));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("TENANT01");
    }
}
