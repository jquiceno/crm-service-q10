using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Infrastructure.Persistence.EntityFramework.Activities.Mappers;
using IntegrationTests.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace IntegrationTests.Activities.Mapping;

/// <summary>
/// F2.2 "Hecho cuando": the same mapping materializes rows correctly on both measured schema
/// variants of <c>tbl_opo_negocios_actividades</c> (Discovery §4.1-bis) — the universal 15
/// columns with <c>varchar(MAX)</c>, and the extended 16 columns with <c>varchar(2000)</c> plus
/// <c>ConsecutivoActMiG</c> — including a deliberately different physical column order, which is
/// also part of the real drift. Reads and writes go through <see cref="ActivityRepositoryMapper"/>,
/// the same boundary the repository (F2.4) will use.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ActivityMappingTests : IAsyncLifetime
{
    private const string InsertRawRowSql = """
        INSERT INTO dbo.tbl_opo_negocios_actividades
            (negact_neg_consecutivo, negact_opo_consecutivo, negact_tipo, negact_titulo,
             negact_descripcion, negact_resultado, negact_fecha, negact_fecha_vencimiento,
             negact_completada, negact_anulada, negact_fecha_resuelto, negact_asesor,
             negact_per_codigo)
        OUTPUT INSERTED.negact_consecutivoP
        VALUES
            (@deal, @opportunity, @type, @title, @description, @outcomeType, @date, NULL,
             @completed, @cancelled, NULL, @advisor, @creator);
        """;

    private const string SelectRawRowSql =
        "SELECT negact_tipo, negact_resultado, negact_completada, negact_anulada " +
        "FROM dbo.tbl_opo_negocios_actividades WHERE negact_consecutivoP = @id;";

    private const string SelectPerTenantColumnSql =
        "SELECT ConsecutivoActMiG FROM dbo.tbl_opo_negocios_actividades WHERE negact_consecutivoP = @id;";

    private static readonly DateTime Now = new(2026, 8, 21, 10, 30, 0, DateTimeKind.Unspecified);

    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    public static TheoryData<string> Variants => ActivitySchemaVariants.Variants;

    private readonly SqlServerContainerFixture _fixture;

    public ActivityMappingTests(SqlServerContainerFixture fixture)
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

    // --- Round-trips through the mapper ------------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AScheduledActivity_RoundTripsOnBothVariants(string variant)
    {
        var dueAt = Now.AddDays(1);
        var before = DateTime.UtcNow.AddSeconds(-1);
        var scheduled = Activity.Schedule(
            1200, 845, ActivityType.Call, Description.Create("call the applicant").Value, dueAt,
            Advisor, Creator).Value;

        var id = await SaveAsync(variant, scheduled).ConfigureAwait(true);
        id.ShouldBeGreaterThan(0, "the legacy identity column generates the id");

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Id.ShouldBe(id);
        activity.Status.ShouldBe(ActivityStatus.Scheduled);
        activity.DealId.ShouldBe(1200);
        activity.OpportunityId.ShouldBe(845);
        activity.Type.ShouldBe(ActivityType.Call);
        activity.Description!.Value.ShouldBe("call the applicant");
        activity.DueAt.ShouldBe(dueAt);
        // CreatedAt is stamped by Created() with the real UTC clock; the ±1s margin absorbs the
        // legacy datetime column rounding (1/300s).
        activity.CreatedAt.ShouldNotBeNull();
        activity.CreatedAt!.Value.ShouldBeInRange(before, DateTime.UtcNow.AddSeconds(1));
        activity.Outcome.ShouldBeNull();
        activity.OutcomeType.ShouldBeNull();
        activity.CompletedAt.ShouldBeNull();
        activity.AdvisorId!.Value.ShouldBe("advisor-01");
        activity.CreatedById.Value.ShouldBe("creator-01");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ACompletedCall_RoundTrips_AndWritesTheLegacyChars(string variant)
    {
        var completed = Activity.RegisterCompleted(
            1200, 845, ActivityType.Call, Outcome.Create("the applicant answered").Value,
            OutcomeType.ForCall(CallOutcome.Contacted).Value, dueAt: null, Advisor, Creator, Now).Value;

        var id = await SaveAsync(variant, completed).ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);
        activity.Status.ShouldBe(ActivityStatus.Completed);
        activity.Outcome!.Value.ShouldBe("the applicant answered");
        activity.OutcomeType!.Scope.ShouldBe(ActivityType.Call);
        activity.OutcomeType.Name.ShouldBe(nameof(CallOutcome.Contacted));
        activity.CompletedAt.ShouldBe(Now);
        activity.Description.ShouldBeNull();

        // The chars on disk are the legacy ones, not the domain enum values (DEC-15), and new
        // rows carry real booleans, never NULL bits (production parity: 0 NULLs in real data).
        var (typeChar, outcomeChar, isCompleted, isCancelled) =
            await ReadRawRowAsync(variant, id).ConfigureAwait(true);
        typeChar.ShouldBe("1");
        outcomeChar.ShouldBe("6");
        isCompleted.ShouldBe(true);
        isCancelled.ShouldBe(false);
    }

    // --- Legacy rows written by the monolith ------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheAmbiguousChar3_ResolvesPerRowType(string variant)
    {
        var callRowId = await InsertRawAsync(variant, type: "1", outcomeType: "3", completed: true)
            .ConfigureAwait(true);
        var meetingRowId = await InsertRawAsync(variant, type: "5", outcomeType: "3", completed: true)
            .ConfigureAwait(true);

        var call = await ReadAsync(variant, callRowId).ConfigureAwait(true);
        var meeting = await ReadAsync(variant, meetingRowId).ConfigureAwait(true);

        call.OutcomeType!.Name.ShouldBe(nameof(CallOutcome.WrongNumber));
        meeting.OutcomeType!.Name.ShouldBe(nameof(MeetingOutcome.DealClosed));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AVirtualMeetingRow_KeepsItsMeetingOutcome(string variant)
    {
        // The monolith writes and reads outcome codes on virtual meetings with the meeting
        // catalogue; the service must not lose them on read.
        var id = await InsertRawAsync(variant, type: "6", outcomeType: "1", completed: true)
            .ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Type.ShouldBe(ActivityType.VirtualMeeting);
        activity.OutcomeType!.Name.ShouldBe(nameof(MeetingOutcome.Held));
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AStrayOutcomeCode_OnATypeWithoutACatalogue_IsDiscardedOnRead(string variant)
    {
        // An email row with a stray code: noise the legacy never interpreted (GAP-8 evidence).
        var id = await InsertRawAsync(variant, type: "2", outcomeType: "6", completed: true)
            .ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Type.ShouldBe(ActivityType.Email);
        activity.OutcomeType.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AMigratedHistoricRow_ReadsAsScheduled_WithItsRealType(string variant)
    {
        // NULL status bits, no title, no due date, no advisor: the migrated-history shape
        // (DEC-6 — valid data, not missing data). Type '3' is the legacy meeting.
        var id = await InsertRawAsync(
                variant, type: "3", outcomeType: null, completed: null, cancelled: null,
                title: null, advisor: null)
            .ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Status.ShouldBe(ActivityStatus.Scheduled, "NULL bits read as not completed / not cancelled");
        activity.Type.ShouldBe(ActivityType.LegacyMeeting, "historic rows keep their real type (DEC-5)");
        activity.Description.ShouldBeNull();
        activity.DueAt.ShouldBeNull();
        activity.OutcomeType.ShouldBeNull();
        activity.AdvisorId.ShouldBeNull("migrated history exists without an advisor (§4.1)");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task ACancelledRow_ReadsAsCancelled(string variant)
    {
        var id = await InsertRawAsync(variant, type: "6", outcomeType: null, completed: false, cancelled: true)
            .ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Status.ShouldBe(ActivityStatus.Cancelled);
        activity.Type.ShouldBe(ActivityType.VirtualMeeting);
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AMonolithAnnulledRow_ReadsAsCancelled(string variant)
    {
        // The monolith annuls with completada=1 AND anulada=1: the cancelled bit must win.
        var id = await InsertRawAsync(variant, type: "5", outcomeType: "2", completed: true, cancelled: true)
            .ConfigureAwait(true);

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Status.ShouldBe(ActivityStatus.Cancelled);
        activity.OutcomeType!.Name.ShouldBe(nameof(MeetingOutcome.Cancelled));
    }

    // --- Drift and unknown codes fail loudly ------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AnUnknownTypeChar_FailsExplicitly_NamingTheColumn(string variant)
    {
        var id = await InsertRawAsync(variant, type: "9", outcomeType: null, completed: null)
            .ConfigureAwait(true);

        // The raw row materializes fine as an entity; the explicit failure (D20) happens at the
        // mapper boundary, where the service refuses to guess what it does not recognize.
        var entity = await ReadEntityAsync(variant, id).ConfigureAwait(true);
        var exception = Should.Throw<InvalidOperationException>(
            () => ActivityRepositoryMapper.ToDomain(entity));

        exception.Message.ShouldContain("negact_tipo");
        exception.Message.ShouldContain("'9'");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AnUnknownOutcomeChar_FailsExplicitly_NamingTheColumn(string variant)
    {
        // '4' is the hole of the legacy call catalogue.
        var id = await InsertRawAsync(variant, type: "1", outcomeType: "4", completed: true)
            .ConfigureAwait(true);

        var entity = await ReadEntityAsync(variant, id).ConfigureAwait(true);
        var exception = Should.Throw<InvalidOperationException>(
            () => ActivityRepositoryMapper.ToDomain(entity));

        exception.Message.ShouldContain("negact_resultado");
        exception.Message.ShouldContain("'4'");
    }

    [Fact]
    public async Task ANullDealId_FailsExplicitly_NamingTheProperty()
    {
        // Pins EnableDetailedErrors: negact_neg_consecutivo is nullable in the legacy DB but
        // NOT NULL in the domain (DEC-1); a NULL must fail naming the property, not with a bare
        // "Data is Null".
        var id = await InsertRawAsync(
                ActivitySchemaVariants.Universal15, type: "1", outcomeType: null, completed: null, dealId: null)
            .ConfigureAwait(true);

        var exception = await Record.ExceptionAsync(
                () => ReadEntityAsync(ActivitySchemaVariants.Universal15, id))
            .ConfigureAwait(true);

        exception.ShouldNotBeNull();
        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
            messages += current.Message + " ";
        messages.ShouldContain(nameof(ActivityEntity.DealId));
    }

    [Fact]
    public async Task ThePerTenantColumn_IsNeverTouched()
    {
        // The insert itself proves EF sends no value for ConsecutivoActMiG; this pins it.
        var completed = Activity.RegisterCompleted(
            1200, 845, ActivityType.Note, Outcome.Create("noted").Value, outcomeType: null,
            dueAt: null, Advisor, Creator, Now).Value;

        var id = await SaveAsync(ActivitySchemaVariants.Extended16, completed).ConfigureAwait(true);

        var connection = new SqlConnection(
            ActivitySchemaVariants.ConnectionString(_fixture, ActivitySchemaVariants.Extended16));
        await using (connection.ConfigureAwait(true))
        {
            await connection.OpenAsync().ConfigureAwait(true);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(true))
            {
                command.CommandText = SelectPerTenantColumnSql;
                command.Parameters.AddWithValue("@id", id);

                (await command.ExecuteScalarAsync().ConfigureAwait(true)).ShouldBe(DBNull.Value);
            }
        }
    }

    // --- Plumbing ----------------------------------------------------------------------------

    private async Task<int> SaveAsync(string variant, Activity activity)
    {
        var entity = ActivityRepositoryMapper.ToEntity(activity);

        var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await using (context.ConfigureAwait(false))
        {
            context.Activities.Add(entity);
            await context.SaveChangesAsync().ConfigureAwait(false);
            return entity.Id;
        }
    }

    private async Task<Activity> ReadAsync(string variant, int id)
    {
        var entity = await ReadEntityAsync(variant, id).ConfigureAwait(false);
        return ActivityRepositoryMapper.ToDomain(entity);
    }

    private async Task<ActivityEntity> ReadEntityAsync(string variant, int id)
    {
        var context = ActivitySchemaVariants.CreateContext(_fixture, variant);
        await using (context.ConfigureAwait(false))
        {
            return await context.Activities.AsNoTracking()
                .SingleAsync(e => e.Id == id).ConfigureAwait(false);
        }
    }

    private async Task<(string TypeChar, string? OutcomeChar, bool? IsCompleted, bool? IsCancelled)>
        ReadRawRowAsync(string variant, int id)
    {
        var connection = new SqlConnection(ActivitySchemaVariants.ConnectionString(_fixture, variant));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = SelectRawRowSql;
                command.Parameters.AddWithValue("@id", id);

                var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    (await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
                    var typeChar = reader.GetString(0);
                    var outcomeChar = await reader.IsDBNullAsync(1).ConfigureAwait(false)
                        ? null
                        : reader.GetString(1);
                    bool? isCompleted = await reader.IsDBNullAsync(2).ConfigureAwait(false)
                        ? null
                        : reader.GetBoolean(2);
                    bool? isCancelled = await reader.IsDBNullAsync(3).ConfigureAwait(false)
                        ? null
                        : reader.GetBoolean(3);
                    return (typeChar, outcomeChar, isCompleted, isCancelled);
                }
            }
        }
    }

    private async Task<int> InsertRawAsync(
        string variant,
        string type,
        string? outcomeType,
        bool? completed,
        bool? cancelled = false,
        string? title = "a legacy title",
        string? advisor = "advisor-01",
        int? dealId = 1200)
    {
        var connection = new SqlConnection(ActivitySchemaVariants.ConnectionString(_fixture, variant));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = InsertRawRowSql;
                command.Parameters.AddWithValue(
                    "@deal", dealId.HasValue ? dealId.Value : DBNull.Value);
                command.Parameters.AddWithValue("@opportunity", 845);
                command.Parameters.AddWithValue("@type", type);
                command.Parameters.AddWithValue("@title", (object?)title ?? DBNull.Value);
                command.Parameters.AddWithValue(
                    "@description", outcomeType is null ? DBNull.Value : "a legacy outcome text");
                command.Parameters.AddWithValue("@outcomeType", (object?)outcomeType ?? DBNull.Value);
                command.Parameters.AddWithValue("@date", Now);
                command.Parameters.AddWithValue(
                    "@completed", completed.HasValue ? completed.Value : DBNull.Value);
                command.Parameters.AddWithValue(
                    "@cancelled", cancelled.HasValue ? cancelled.Value : DBNull.Value);
                command.Parameters.AddWithValue("@advisor", (object?)advisor ?? DBNull.Value);
                command.Parameters.AddWithValue("@creator", "creator-01");

                return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
            }
        }
    }
}
