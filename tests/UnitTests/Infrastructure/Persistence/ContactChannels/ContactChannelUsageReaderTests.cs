using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.ContactChannels;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.ContactChannels;

public sealed class ContactChannelUsageReaderTests
{
    private static ApplicationDbContext CreateInMemoryContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static IEntityType GetUsageEntityType()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=ContactChannelModel;Trusted_Connection=True;")
            .Options;

        using var context = new ApplicationDbContext(options);

        var entityType = context.Model.FindEntityType(typeof(ContactChannelUsage));
        entityType.ShouldNotBeNull();

        return entityType;
    }

    [Fact]
    public void Configure_ReadsTheOpportunitiesTableOfTheLegacySchema()
    {
        GetUsageEntityType().GetTableName().ShouldBe("tbl_opo_oportunidades");
    }

    [Fact]
    public void Configure_MapsOnlyTheForeignKeyColumn()
    {
        var entityType = GetUsageEntityType();

        var contactChannelId = entityType.FindProperty(nameof(ContactChannelUsage.ContactChannelId));
        contactChannelId.ShouldNotBeNull();
        contactChannelId.GetColumnName().ShouldBe("opo_medcon_consecutivo");

        entityType.GetProperties().Select(p => p.Name)
            .ShouldBe([nameof(ContactChannelUsage.ContactChannelId)]);
    }

    // Keyless keeps this context from owning a table that belongs to another aggregate: it can be
    // read and never written, and it declares no navigation towards the channel.
    [Fact]
    public void Configure_DeclaresNoKeyAndNoNavigation()
    {
        var entityType = GetUsageEntityType();

        entityType.FindPrimaryKey().ShouldBeNull();
        entityType.GetNavigations().ShouldBeEmpty();
        entityType.GetForeignKeys().ShouldBeEmpty();
    }

    [Fact]
    public async Task IsReferencedAsync_WhenNoOpportunityPointsAtTheChannel_SucceedsWithFalse()
    {
        using var context = CreateInMemoryContext(
            nameof(IsReferencedAsync_WhenNoOpportunityPointsAtTheChannel_SucceedsWithFalse));
        var sut = new ContactChannelUsageReader(
            context,
            Substitute.For<ILoggerPort<ContactChannelUsageReader>>());

        var result = await sut.IsReferencedAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task IsReferencedAsync_WhenThePersistenceIsGone_ReturnsInternalErrorAndLogs()
    {
        var context = CreateInMemoryContext(
            nameof(IsReferencedAsync_WhenThePersistenceIsGone_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelUsageReader>>();
        var sut = new ContactChannelUsageReader(context, logger);
        await context.DisposeAsync();

        var result = await sut.IsReferencedAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(nameof(ContactChannelUsageReader));
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
