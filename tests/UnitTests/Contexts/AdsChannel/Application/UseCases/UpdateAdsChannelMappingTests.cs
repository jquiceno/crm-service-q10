using AdsChannel.Application.UseCases.UpdateAdsChannel;
using AdsChannel.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class UpdateAdsChannelMappingTests
{
    [Fact]
    public void ToUpdateArgs_PreservesFields()
    {
        var input = new UpdateAdsChannelInputDto("Google Ads", true);

        var args = input.ToUpdateArgs();

        args.Name.ShouldBe("Google Ads");
        args.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToOutputDto_PreservesAggregateFields()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Meta Ads", false);

        var dto = aggregate.ToOutputDto();

        dto.Id.ShouldBe(1);
        dto.Name.ShouldBe("Meta Ads");
        dto.IsActive.ShouldBeFalse();
    }
}
