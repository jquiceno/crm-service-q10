using Infrastructure.Persistence.EntityFramework.Activities;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace IntegrationTests.Activities;

/// <summary>
/// Readers of the Activities context against the legacy foreign tables.
/// </summary>
/// <remarks>
/// The container schema comes from the EF model (<c>EnsureCreated</c>), so these tests prove the
/// reader logic and the mapping as configured. They do <b>not</b> prove the mapping matches the
/// real institutions — that is the multi-variant drift verification of a later task.
/// <para>
/// Seeding goes through the ORM, like the mapping tests of the sibling task: a renamed property or
/// a changed column mapping then breaks the compilation instead of failing at runtime with a stale
/// SQL string.
/// </para>
/// </remarks>
[Collection(IntegrationTestCollection.Name)]
public sealed class ReadersTests(SqlServerContainerFixture fixture) : IntegrationTestBase(fixture)
{
    private const int DealId = 1200;
    private const int OpportunityId = 845;
    private const int DealStateId = 3;
    private const string PersonCode = "339968541842";
    private const string Identification = "1017123456";

    // --- IDealReader ---------------------------------------------------------------------

    [Fact]
    public async Task GetDealContextAsync_WithAnExistingDeal_ReturnsItsOpportunity()
    {
        await SeedDealAsync(archived: false);

        var context = await new DealReader(Db).GetDealContextAsync(DealId);

        context.Exists.ShouldBeTrue();
        context.OpportunityId.ShouldBe(OpportunityId);
        context.OpportunityArchived.ShouldBeFalse();
    }

    [Fact]
    public async Task GetDealContextAsync_WithAnArchivedOpportunity_ReportsItArchived()
    {
        await SeedDealAsync(archived: true);

        var context = await new DealReader(Db).GetDealContextAsync(DealId);

        context.Exists.ShouldBeTrue();
        context.OpportunityArchived.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDealContextAsync_WhenTheArchivedFlagIsNull_ReportsItNotArchived()
    {
        // The column is bit NULL and every legacy procedure reads it as ISNULL(opo_estado, 0).
        await SeedDealAsync(archived: null);

        var context = await new DealReader(Db).GetDealContextAsync(DealId);

        context.Exists.ShouldBeTrue();
        context.OpportunityArchived.ShouldBeFalse();
    }

    [Fact]
    public async Task GetDealContextAsync_WithAnUnknownDeal_ReturnsNotFound()
    {
        await SeedDealAsync(archived: false);

        var context = await new DealReader(Db).GetDealContextAsync(dealId: 999999);

        context.Exists.ShouldBeFalse();
        context.OpportunityId.ShouldBeNull();
    }

    // --- IAdvisorReader ------------------------------------------------------------------

    [Fact]
    public async Task ResolveByIdentificationAsync_WithAnExistingIdentification_ReturnsThePersonCode()
    {
        await SeedPersonAsync();

        var code = await new AdvisorReader(Db).ResolveByIdentificationAsync(Identification);

        code.ShouldBe(PersonCode);
    }

    [Fact]
    public async Task ResolveByIdentificationAsync_TrimsTheInput()
    {
        await SeedPersonAsync();

        var code = await new AdvisorReader(Db).ResolveByIdentificationAsync($"  {Identification}  ");

        code.ShouldBe(PersonCode);
    }

    [Fact]
    public async Task ResolveByIdentificationAsync_WithAnUnknownIdentification_ReturnsNull()
    {
        await SeedPersonAsync();

        var code = await new AdvisorReader(Db).ResolveByIdentificationAsync("0000000000");

        code.ShouldBeNull("a reader finding nothing is a valid outcome, not a failure");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveByIdentificationAsync_WithBlankInput_ReturnsNull(string? identification)
    {
        var code = await new AdvisorReader(Db).ResolveByIdentificationAsync(identification);

        code.ShouldBeNull();
    }

    // --- Seeding -------------------------------------------------------------------------

    private async Task SeedDealAsync(bool? archived)
    {
        Db.Set<Opportunity>().Add(new Opportunity
        {
            Id = OpportunityId,
            Name = "Oportunidad de prueba",
            IsArchived = archived,
        });

        Db.Set<Deal>().Add(new Deal
        {
            Id = DealId,
            OpportunityId = OpportunityId,
            DealStateId = DealStateId,
            Name = "Negocio de prueba",
        });

        await Db.SaveChangesAsync().ConfigureAwait(false);
    }

    private async Task SeedPersonAsync()
    {
        Db.Set<Person>().Add(new Person
        {
            Code = PersonCode,
            Identification = Identification,
            FullName = "Ana Gómez",
        });

        await Db.SaveChangesAsync().ConfigureAwait(false);
    }
}
