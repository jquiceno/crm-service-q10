using AdsChannel.Domain.Aggregates;
using Infrastructure.Persistence.EntityFramework.AdsChannels.Mappers;
using Shouldly;
using Xunit;
using AdsChannelDocument = Infrastructure.Persistence.EntityFramework.AdsChannels.Entities.AdsChannel;

namespace UnitTests.Infrastructure.Persistence.AdsChannels;

public sealed class AdsChannelRepositoryMapperTests
{
    [Fact]
    public void ToDomain_MapsEveryFieldFromTheDocument()
    {
        var document = new AdsChannelDocument
        {
            Id = 7,
            Name = "Google Ads",
            IsActive = false
        };

        var aggregate = AdsChannelRepositoryMapper.ToDomain(document);

        aggregate.Id.ShouldBe(7);
        aggregate.Name.ShouldBe("Google Ads");
        aggregate.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void ToDomain_WithNullNameAndIsActive_FallsBackToReconstructDefaults()
    {
        var document = new AdsChannelDocument
        {
            Id = 7,
            Name = null,
            IsActive = null
        };

        var aggregate = AdsChannelRepositoryMapper.ToDomain(document);

        aggregate.Name.ShouldBe(string.Empty);
        aggregate.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ToDocument_MapsEveryFieldFromTheAggregate()
    {
        var aggregate = AdsChannelAggregate.Reconstruct(7, "Meta Ads", false);

        var document = AdsChannelRepositoryMapper.ToDocument(aggregate);

        document.Id.ShouldBe(7);
        document.Name.ShouldBe("Meta Ads");
        document.IsActive.ShouldBe(false);
    }
}
