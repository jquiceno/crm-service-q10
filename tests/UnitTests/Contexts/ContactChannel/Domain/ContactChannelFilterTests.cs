using ContactChannel.Domain.Queries;
using Shouldly;
using Xunit;

namespace UnitTests.Contexts.ContactChannel.Domain;

public sealed class ContactChannelFilterTests
{
    [Fact]
    public void Filter_WithoutValues_MeansNoFiltering()
    {
        var filter = new ContactChannelFilter(IsActive: null, SearchName: null);

        filter.IsActive.ShouldBeNull();
        filter.SearchName.ShouldBeNull();
    }

    [Fact]
    public void Filter_KeepsTheValuesItReceives()
    {
        var filter = new ContactChannelFilter(IsActive: false, SearchName: "wha");

        filter.IsActive.ShouldBe(false);
        filter.SearchName.ShouldBe("wha");
    }
}
