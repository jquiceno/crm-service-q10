using ContactChannel.Domain.Queries;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Domain;

public sealed class ContactChannelFilterTests
{
    [Fact]
    public void Filter_WithoutValues_MeansNoFiltering()
    {
        var filter = new ContactChannelFilter(IsActive: null, Search: null);

        filter.IsActive.ShouldBeNull();
        filter.Search.ShouldBeNull();
    }

    [Fact]
    public void Filter_KeepsTheValuesItReceives()
    {
        var filter = new ContactChannelFilter(IsActive: false, Search: "wha");

        filter.IsActive.ShouldBe(false);
        filter.Search.ShouldBe("wha");
    }
}
