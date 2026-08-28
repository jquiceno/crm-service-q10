using AdsChannel.Application.UseCases.CreateAdsChannel;
using AdsChannel.Domain.Aggregates;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.AdsChannel.Application.UseCases;

public sealed class CreateAdsChannelMappingTests
{
    [Fact]
    public void ToAggregate_WithValidInput_PreservesFields()
    {
        var input = new CreateAdsChannelInputDto("Google Ads", true);

        var result = input.ToAggregate();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Google Ads");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToAggregate_WithInvalidInput_PropagatesTheDomainFailure()
    {
        var input = new CreateAdsChannelInputDto("", true);

        var result = input.ToAggregate();

        result.IsFailure.ShouldBeTrue();
    }

    [Fact]
    public void ToOutputDto_PreservesAggregateFields()
    {
        var aggregate = AdsChannelAggregate.Create(new CreateAdsChannelArgs("Meta Ads", false)).Value;

        var dto = aggregate.ToOutputDto();

        dto.Id.ShouldBe(aggregate.Id);
        dto.Name.ShouldBe("Meta Ads");
        dto.IsActive.ShouldBeFalse();
    }
}
