using FluentAssertions;
using Infrastructure.Cache;
using Xunit;

namespace ServiceTemplate.Tests.Cache;

public sealed class NullCacheServiceTests
{
    private readonly NullCacheService _sut = new();

    [Fact]
    public async Task GetAsync_AlwaysReturnsDefault()
    {
        var result = await _sut.GetAsync<string>("any-key");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_ForValueType_ReturnsDefaultValue()
    {
        var result = await _sut.GetAsync<int>("any-key");

        result.Should().Be(0);
    }

    [Fact]
    public async Task SetAsync_DoesNotThrow()
    {
        var act = () => _sut.SetAsync("key", "value", TimeSpan.FromSeconds(60));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetAsync_AfterSet_StillReturnsDefault()
    {
        // NullCacheService discards all writes
        await _sut.SetAsync("key", "stored-value", TimeSpan.FromSeconds(60));
        var result = await _sut.GetAsync<string>("key");

        result.Should().BeNull("NullCacheService never stores anything");
    }

    [Fact]
    public async Task RemoveAsync_DoesNotThrow()
    {
        var act = () => _sut.RemoveAsync("any-key");

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task RemoveByPrefixAsync_DoesNotThrow()
    {
        var act = () => _sut.RemoveByPrefixAsync("any:prefix:");

        await act.Should().NotThrowAsync();
    }
}
