using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Activities;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace IntegrationTests.Activities;

/// <summary>
/// F2.6: readers of the Activities context, proven against both measured schema variants
/// (Discovery §4.1-bis), not just the EF-model shape.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ReadersTests : IAsyncLifetime
{
    private const int DealId = 1200;
    private const int OpportunityId = 845;
    private const int DealStateId = 3;
    private const string PersonCode = "339968541842";
    private const string Identification = "1017123456";

    public static TheoryData<string> Variants => ActivitySchemaVariants.Variants;

    private readonly SqlServerContainerFixture _fixture;

    public ReadersTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await ActivitySchemaVariants.EnsureCreatedAsync(_fixture, ActivitySchemaVariants.Universal15)
            .ConfigureAwait(false);
        await ActivitySchemaVariants.EnsureCreatedAsync(_fixture, ActivitySchemaVariants.Extended16)
            .ConfigureAwait(false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- IDealReader ---------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GetDealContextAsync_WithAnExistingDeal_ReturnsItsOpportunity(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, archived: false).ConfigureAwait(true);

        var result = await new DealReader(context).GetDealContextAsync(DealId).ConfigureAwait(true);

        result.Exists.ShouldBeTrue();
        result.OpportunityId.ShouldBe(OpportunityId);
        result.OpportunityArchived.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GetDealContextAsync_WithAnArchivedOpportunity_ReportsItArchived(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, archived: true).ConfigureAwait(true);

        var result = await new DealReader(context).GetDealContextAsync(DealId).ConfigureAwait(true);

        result.Exists.ShouldBeTrue();
        result.OpportunityArchived.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GetDealContextAsync_WhenTheArchivedFlagIsNull_ReportsItNotArchived(string variant)
    {
        // The column is bit NULL and every legacy procedure reads it as ISNULL(opo_estado, 0).
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, archived: null).ConfigureAwait(true);

        var result = await new DealReader(context).GetDealContextAsync(DealId).ConfigureAwait(true);

        result.Exists.ShouldBeTrue();
        result.OpportunityArchived.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task GetDealContextAsync_WithAnUnknownDeal_ReturnsNotFound(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedDealAsync(context, archived: false).ConfigureAwait(true);

        var result = await new DealReader(context).GetDealContextAsync(dealId: 999999).ConfigureAwait(true);

        result.Exists.ShouldBeFalse();
        result.OpportunityId.ShouldBeNull();
    }

    // --- IAdvisorReader ------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ResolveByIdentificationAsync_WithAnExistingIdentification_ReturnsThePersonCode(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedPersonAsync(context).ConfigureAwait(true);

        var code = await new AdvisorReader(context)
            .ResolveByIdentificationAsync(Identification).ConfigureAwait(true);

        code.ShouldBe(PersonCode);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ResolveByIdentificationAsync_TrimsTheInput(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedPersonAsync(context).ConfigureAwait(true);

        var code = await new AdvisorReader(context)
            .ResolveByIdentificationAsync($"  {Identification}  ").ConfigureAwait(true);

        code.ShouldBe(PersonCode);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ResolveByIdentificationAsync_WithAnUnknownIdentification_ReturnsNull(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await SeedPersonAsync(context).ConfigureAwait(true);

        var code = await new AdvisorReader(context)
            .ResolveByIdentificationAsync("0000000000").ConfigureAwait(true);

        code.ShouldBeNull("a reader finding nothing is a valid outcome, not a failure");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ResolveByIdentificationAsync_WithBlankInput_ReturnsNull(string variant)
    {
        using var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        var reader = new AdvisorReader(context);

        (await reader.ResolveByIdentificationAsync(null).ConfigureAwait(true)).ShouldBeNull();
        (await reader.ResolveByIdentificationAsync("").ConfigureAwait(true)).ShouldBeNull();
        (await reader.ResolveByIdentificationAsync("   ").ConfigureAwait(true)).ShouldBeNull();
    }

    // --- Seeding -------------------------------------------------------------------------

    private static async Task SeedDealAsync(ApplicationDbContext context, bool? archived)
    {
        context.Set<Opportunity>().Add(new Opportunity
        {
            Id = OpportunityId,
            Name = "Oportunidad de prueba",
            IsArchived = archived,
        });

        context.Set<Deal>().Add(new Deal
        {
            Id = DealId,
            OpportunityId = OpportunityId,
            DealStateId = DealStateId,
            Name = "Negocio de prueba",
        });

        await context.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task SeedPersonAsync(ApplicationDbContext context)
    {
        context.Set<Person>().Add(new Person
        {
            Code = PersonCode,
            Identification = Identification,
            FullName = "Ana Gómez",
        });

        await context.SaveChangesAsync().ConfigureAwait(false);
    }
}
