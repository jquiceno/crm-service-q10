using Infrastructure.Caching;
using Shared.Results;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure;

public sealed class NoOpCacheStoreTests
{
    private readonly NoOpCacheStore _sut = new();

    [Fact]
    public async Task GetOrSetAsync_ReturnsFactorySuccess()
    {
        var result = await _sut.GetOrSetAsync(
            "ctx:t:v1:x:1", TimeSpan.FromMinutes(1),
            () => Task.FromResult(Result<int>.Success(7)));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(7);
    }

    [Fact]
    public async Task GetOrSetAsync_PassesThroughFactoryFailure()
    {
        var error = new DomainError("boom", ErrorType.Internal);

        var result = await _sut.GetOrSetAsync<int>(
            "ctx:t:v1:x:1", TimeSpan.FromMinutes(1),
            () => Task.FromResult(Result<int>.Failure(error)));

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(error);
    }

    [Fact]
    public async Task RemoveAsync_And_RemoveByPrefixAsync_DoNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.RemoveAsync("ctx:t:v1:x:1"));
        await Should.NotThrowAsync(() => _sut.RemoveByPrefixAsync("ctx:t:v1:x:list"));
    }
}
