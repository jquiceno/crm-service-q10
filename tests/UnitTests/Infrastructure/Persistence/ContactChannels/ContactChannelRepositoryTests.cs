using ContactChannel.Domain.Aggregates;
using ContactChannel.Domain.Queries;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.ContactChannels;
using Infrastructure.Persistence.EntityFramework.ContactChannels.Entities;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shared.Application.Ports;
using Shared.Domain.Pagination;
using Shared.Results.Errors;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Persistence.ContactChannels;

public sealed class ContactChannelRepositoryTests
{
    private static readonly ContactChannelFilter NoFilter = new(IsActive: null, SearchName: null);

    private static ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        return new ApplicationDbContext(options);
    }

    private static ContactChannelRepository CreateRepository(
        ApplicationDbContext context,
        ILoggerPort<ContactChannelRepository>? logger = null) =>
        new(context, logger ?? Substitute.For<ILoggerPort<ContactChannelRepository>>());

    private static async Task SeedAsync(string dbName, params ContactChannelEntity[] documents)
    {
        using var context = CreateContext(dbName);
        context.ContactChannels.AddRange(documents);
        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static ContactChannelEntity Row(int id, string? name, bool? isActive) =>
        new() { Id = id, Name = name, IsActive = isActive };

    [Fact]
    public async Task GetByIdAsync_WhenTheRowExists_ReturnsTheAggregate()
    {
        const string dbName = nameof(GetByIdAsync_WhenTheRowExists_ReturnsTheAggregate);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).GetByIdAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(7);
        result.Value.Name.ShouldBe("WhatsApp");
        result.Value.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WithAnUnknownId_FailsAsNotFoundStampedWithTheOrigin()
    {
        using var context = CreateContext(nameof(GetByIdAsync_WithAnUnknownId_FailsAsNotFoundStampedWithTheOrigin));

        var result = await CreateRepository(context).GetByIdAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Message.ShouldContain("404");
        result.Error.Origin.ShouldBe(nameof(ContactChannelRepository));
    }

    [Fact]
    public async Task GetByIdAsync_DoesNotTrackTheRowItReads()
    {
        const string dbName = nameof(GetByIdAsync_DoesNotTrackTheRowItReads);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        await CreateRepository(context).GetByIdAsync(7);

        context.ChangeTracker.Entries<ContactChannelEntity>().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetByIdAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetByIdAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task ExistsAsync_WhenTheRowExists_SucceedsWithTrue()
    {
        const string dbName = nameof(ExistsAsync_WhenTheRowExists_SucceedsWithTrue);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).ExistsAsync(7);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_WithAnUnknownId_SucceedsWithFalse()
    {
        using var context = CreateContext(nameof(ExistsAsync_WithAnUnknownId_SucceedsWithFalse));

        var result = await CreateRepository(context).ExistsAsync(404);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(ExistsAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.ExistsAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task GetAsync_WithoutFilters_ReturnsActiveAndInactiveChannels()
    {
        const string dbName = nameof(GetAsync_WithoutFilters_ReturnsActiveAndInactiveChannels);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", false));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).GetAsync(NoFilter, new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(c => c.Name).ShouldBe(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task GetAsync_FilteringByActive_ReturnsOnlyActiveChannels()
    {
        const string dbName = nameof(GetAsync_FilteringByActive_ReturnsOnlyActiveChannels);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", false), Row(3, "Gamma", null));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: true, SearchName: null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(1);
        result.Items.Select(c => c.Name).ShouldBe(["Alpha"]);
    }

    [Fact]
    public async Task GetAsync_FilteringByInactive_AlsoReturnsRowsWithANullState()
    {
        const string dbName = nameof(GetAsync_FilteringByInactive_AlsoReturnsRowsWithANullState);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", false), Row(3, "Gamma", null));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: false, SearchName: null), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(2);
        result.Items.Select(c => c.Name).ShouldBe(["Beta", "Gamma"]);
    }

    [Fact]
    public async Task GetAsync_FilteringByName_MatchesOnASubstring()
    {
        const string dbName = nameof(GetAsync_FilteringByName_MatchesOnASubstring);
        await SeedAsync(dbName, Row(1, "WhatsApp", true), Row(2, "Feria", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: null, SearchName: "hats"), new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.Select(c => c.Name).ShouldBe(["WhatsApp"]);
    }

    [Fact]
    public async Task GetAsync_FilteringByName_IgnoresSurroundingWhitespace()
    {
        const string dbName = nameof(GetAsync_FilteringByName_IgnoresSurroundingWhitespace);
        await SeedAsync(dbName, Row(1, "WhatsApp", true), Row(2, "Feria", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: null, SearchName: "  hats  "), new PageQuery(0, 10));

        result.Items.Select(c => c.Name).ShouldBe(["WhatsApp"]);
    }

    [Fact]
    public async Task GetAsync_FilteringByName_NeverMatchesARowWithANullName()
    {
        const string dbName = nameof(GetAsync_FilteringByName_NeverMatchesARowWithANullName);
        await SeedAsync(dbName, Row(1, null, true), Row(2, "Feria", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: null, SearchName: "eri"), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(1);
        result.Items.Select(c => c.Name).ShouldBe(["Feria"]);
    }

    [Fact]
    public async Task GetAsync_WithABlankNameFilter_DoesNotFilter()
    {
        const string dbName = nameof(GetAsync_WithABlankNameFilter_DoesNotFilter);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: null, SearchName: "   "), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithBothFilters_RequiresBothToMatch()
    {
        const string dbName = nameof(GetAsync_WithBothFilters_RequiresBothToMatch);
        await SeedAsync(
            dbName,
            Row(1, "Feria escolar", true),
            Row(2, "Feria universitaria", false),
            Row(3, "Llamada", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context)
            .GetAsync(new ContactChannelFilter(IsActive: true, SearchName: "Feria"), new PageQuery(0, 10));

        result.TotalCount.ShouldBe(1);
        result.Items.Select(c => c.Name).ShouldBe(["Feria escolar"]);
    }

    [Fact]
    public async Task GetAsync_OrdersByNameAndBreaksTiesWithTheIdentifier()
    {
        const string dbName = nameof(GetAsync_OrdersByNameAndBreaksTiesWithTheIdentifier);
        await SeedAsync(dbName, Row(3, "Beta", true), Row(1, "Alpha", true), Row(2, "Beta", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).GetAsync(NoFilter, new PageQuery(0, 10));

        result.Items.Select(c => c.Id).ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task GetAsync_PagesTheResultAndReportsTheFullTotal()
    {
        const string dbName = nameof(GetAsync_PagesTheResultAndReportsTheFullTotal);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", true), Row(3, "Gamma", true));
        using var context = CreateContext(dbName);
        var sut = CreateRepository(context);

        var firstPage = await sut.GetAsync(NoFilter, new PageQuery(0, 2));
        var secondPage = await sut.GetAsync(NoFilter, new PageQuery(1, 2));

        firstPage.TotalCount.ShouldBe(3);
        firstPage.Items.Select(c => c.Name).ShouldBe(["Alpha", "Beta"]);
        secondPage.TotalCount.ShouldBe(3);
        secondPage.Items.Select(c => c.Name).ShouldBe(["Gamma"]);
    }

    [Fact]
    public async Task GetAsync_PastTheLastPage_SucceedsWithNoItemsAndTheFullTotal()
    {
        const string dbName = nameof(GetAsync_PastTheLastPage_SucceedsWithNoItemsAndTheFullTotal);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).GetAsync(NoFilter, new PageQuery(5, 2));

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetAsync_WithoutMatches_SucceedsWithAnEmptyPage()
    {
        using var context = CreateContext(nameof(GetAsync_WithoutMatches_SucceedsWithAnEmptyPage));

        var result = await CreateRepository(context).GetAsync(NoFilter, new PageQuery(0, 10));

        result.IsSuccess.ShouldBeTrue();
        result.Items.ShouldBeEmpty();
        result.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(GetAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.GetAsync(NoFilter, new PageQuery(0, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEveryChannelPaged()
    {
        const string dbName = nameof(GetAllAsync_ReturnsEveryChannelPaged);
        await SeedAsync(dbName, Row(1, "Alpha", true), Row(2, "Beta", false), Row(3, "Gamma", null));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).GetAllAsync(new PageQuery(0, 2));

        result.IsSuccess.ShouldBeTrue();
        result.TotalCount.ShouldBe(3);
        result.Items.Select(c => c.Name).ShouldBe(["Alpha", "Beta"]);
    }

    [Fact]
    public async Task AddAsync_EnqueuesTheInsert()
    {
        using var context = CreateContext(nameof(AddAsync_EnqueuesTheInsert));
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        var result = await CreateRepository(context).AddAsync(aggregate.Value);

        result.IsSuccess.ShouldBeTrue();

        var entry = context.ChangeTracker.Entries<ContactChannelEntity>().ShouldHaveSingleItem();
        entry.State.ShouldBe(EntityState.Added);
        entry.Entity.Name.ShouldBe("WhatsApp");
        entry.Entity.IsActive.ShouldBe(true);
    }

    [Fact]
    public async Task AddAsync_AfterTheCommit_PersistsTheChannel()
    {
        const string dbName = nameof(AddAsync_AfterTheCommit_PersistsTheChannel);
        using var context = CreateContext(dbName);
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        await CreateRepository(context).AddAsync(aggregate.Value);
        await context.SaveChangesAsync();

        using var verifyContext = CreateContext(dbName);
        var stored = await verifyContext.ContactChannels.AsNoTracking().SingleAsync();
        stored.Name.ShouldBe("WhatsApp");
        stored.IsActive.ShouldBe(true);
    }

    [Fact]
    public async Task AddAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(AddAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));
        await context.DisposeAsync();

        var result = await sut.AddAsync(aggregate.Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task CreateAsync_PersistsTheChannelAndReturnsItWithTheGeneratedIdentifier()
    {
        const string dbName = nameof(CreateAsync_PersistsTheChannelAndReturnsItWithTheGeneratedIdentifier);
        using var context = CreateContext(dbName);
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        var result = await CreateRepository(context).CreateAsync(aggregate.Value);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBeGreaterThan(0);
        result.Value.Name.ShouldBe("WhatsApp");
        result.Value.IsActive.ShouldBeTrue();

        using var verifyContext = CreateContext(dbName);
        var stored = await verifyContext.ContactChannels.AsNoTracking().SingleAsync();
        stored.Id.ShouldBe(result.Value.Id);
        stored.Name.ShouldBe("WhatsApp");
        stored.IsActive.ShouldBe(true);
    }

    [Fact]
    public async Task CreateAsync_ReturnsAnAggregateWithoutAuditDates()
    {
        using var context = CreateContext(nameof(CreateAsync_ReturnsAnAggregateWithoutAuditDates));
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        var result = await CreateRepository(context).CreateAsync(aggregate.Value);

        result.Value.CreatedAt.ShouldBeNull();
        result.Value.UpdatedAt.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_AcceptsADuplicateName()
    {
        const string dbName = nameof(CreateAsync_AcceptsADuplicateName);
        await SeedAsync(dbName, Row(1, "WhatsApp", true));
        using var context = CreateContext(dbName);
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        var result = await CreateRepository(context).CreateAsync(aggregate.Value);

        result.IsSuccess.ShouldBeTrue();
        (await context.ContactChannels.CountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_WhenTheCommitFails_ReturnsAPersistenceErrorAndLogs()
    {
        using var context = CreateContext(nameof(CreateAsync_WhenTheCommitFails_ReturnsAPersistenceErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        sut.Update(ContactChannelAggregate.Reconstruct(id: 404, name: "Ghost", isActive: true));
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));

        var result = await sut.CreateAsync(aggregate.Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        result.Error.Origin.ShouldBe(nameof(ContactChannelRepository));
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task CreateAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(CreateAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("WhatsApp", IsActive: true));
        await context.DisposeAsync();

        var result = await sut.CreateAsync(aggregate.Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task Update_PersistsTheNameAndTheState()
    {
        const string dbName = nameof(Update_PersistsTheNameAndTheState);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        var result = CreateRepository(context)
            .Update(ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false));

        result.IsSuccess.ShouldBeTrue();

        await context.SaveChangesAsync();

        using var verifyContext = CreateContext(dbName);
        var stored = await verifyContext.ContactChannels.AsNoTracking().SingleAsync();
        stored.Name.ShouldBe("Feria");
        stored.IsActive.ShouldBe(false);
    }

    [Fact]
    public async Task Update_MarksTheRowAsModifiedAndNeverAsAdded()
    {
        const string dbName = nameof(Update_MarksTheRowAsModifiedAndNeverAsAdded);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        CreateRepository(context).Update(ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false));

        var entry = context.ChangeTracker.Entries<ContactChannelEntity>().ShouldHaveSingleItem();
        entry.State.ShouldBe(EntityState.Modified);
    }

    [Fact]
    public async Task Update_WithAnUnassignedIdentifier_FailsAsNotFoundAndInsertsNothing()
    {
        const string dbName = nameof(Update_WithAnUnassignedIdentifier_FailsAsNotFoundAndInsertsNothing);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var aggregate = ContactChannelAggregate.Create(new CreateContactChannelArgs("Feria", IsActive: true));

        var result = CreateRepository(context, logger).Update(aggregate.Value);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(nameof(ContactChannelRepository));
        context.ChangeTracker.Entries<ContactChannelEntity>().ShouldBeEmpty();
        logger.Received(1).Warning(Arg.Any<string>(), Arg.Any<object[]>());

        await context.SaveChangesAsync();
        (await context.ContactChannels.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Update_WhenTheRowIsAlreadyTracked_UpdatesTheTrackedInstance()
    {
        const string dbName = nameof(Update_WhenTheRowIsAlreadyTracked_UpdatesTheTrackedInstance);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);
        var sut = CreateRepository(context);
        var tracked = await context.ContactChannels.SingleAsync();

        var result = sut.Update(ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false));

        result.IsSuccess.ShouldBeTrue();
        tracked.Name.ShouldBe("Feria");
        tracked.IsActive.ShouldBe(false);
        context.ChangeTracker.Entries<ContactChannelEntity>().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Update_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(Update_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        await context.DisposeAsync();

        var result = sut.Update(ContactChannelAggregate.Reconstruct(id: 7, name: "Feria", isActive: false));

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task RemoveAsync_WhenTheRowExists_MarksItForDeletion()
    {
        const string dbName = nameof(RemoveAsync_WhenTheRowExists_MarksItForDeletion);
        await SeedAsync(dbName, Row(7, "WhatsApp", true));
        using var context = CreateContext(dbName);

        var result = await CreateRepository(context).RemoveAsync(7);

        result.IsSuccess.ShouldBeTrue();

        var entry = context.ChangeTracker.Entries<ContactChannelEntity>().ShouldHaveSingleItem();
        entry.State.ShouldBe(EntityState.Deleted);
        (await context.ContactChannels.CountAsync()).ShouldBe(1);

        await context.SaveChangesAsync();
        (await context.ContactChannels.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task RemoveAsync_WithAnUnknownId_FailsAsNotFoundStampedWithTheOrigin()
    {
        using var context = CreateContext(nameof(RemoveAsync_WithAnUnknownId_FailsAsNotFoundStampedWithTheOrigin));

        var result = await CreateRepository(context).RemoveAsync(404);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.NotFound);
        result.Error.Origin.ShouldBe(nameof(ContactChannelRepository));
    }

    [Fact]
    public async Task RemoveAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs()
    {
        var context = CreateContext(nameof(RemoveAsync_WhenThePersistenceFails_ReturnsInternalErrorAndLogs));
        var logger = Substitute.For<ILoggerPort<ContactChannelRepository>>();
        var sut = CreateRepository(context, logger);
        await context.DisposeAsync();

        var result = await sut.RemoveAsync(7);

        result.IsFailure.ShouldBeTrue();
        result.Error.Type.ShouldBe(ErrorType.Internal);
        logger.Received(1).Error(Arg.Is<Exception?>(e => e != null), Arg.Any<string>(), Arg.Any<object[]>());
    }
}
