using AdsChannel.Application.UseCases.GetAdsChannels;
using AdsChannel.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class GetAdsChannelsMappingTests
{
    [Fact]
    public void ToOutputDto_PreservesAggregateFields()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(1, "Google Ads", true);

        var dto = aggregate.ToOutputDto();

        dto.Id.ShouldBe(1);
        dto.Name.ShouldBe("Google Ads");
        dto.IsActive.ShouldBeTrue();
    }
}
