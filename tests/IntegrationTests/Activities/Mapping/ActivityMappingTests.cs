using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework;
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
/// also part of the real drift.
/// </summary>
[Collection(IntegrationTestCollection.Name)]
public sealed class ActivityMappingTests : IAsyncLifetime
{
    private const string Universal15 = "activities_mapping_universal15";
    private const string Extended16 = "activities_mapping_extended16";

    private const string Universal15Ddl = """
        CREATE TABLE dbo.tbl_opo_negocios_actividades (
            negact_consecutivoP int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            negact_neg_consecutivo int NULL,
            negact_opo_consecutivo int NULL,
            negact_per_codigo varchar(20) NOT NULL,
            negact_asesor varchar(20) NULL,
            negact_tipo char(1) NOT NULL,
            negact_fecha datetime NOT NULL,
            negact_titulo varchar(500) NULL,
            negact_descripcion varchar(MAX) NULL,
            negact_resultado char(1) NULL,
            negact_fecha_vencimiento datetime NULL,
            negact_completada bit NULL,
            negact_anulada bit NULL,
            negact_fecha_resuelto datetime NULL,
            negact_descripcion_virtual varchar(500) NULL)
        """;

    private const string Extended16Ddl = """
        CREATE TABLE dbo.tbl_opo_negocios_actividades (
            negact_consecutivoP int IDENTITY(1,1) NOT NULL PRIMARY KEY,
            negact_neg_consecutivo int NULL,
            negact_opo_consecutivo int NULL,
            negact_tipo char(1) NOT NULL,
            negact_titulo varchar(500) NULL,
            negact_descripcion varchar(2000) NULL,
            negact_resultado char(1) NULL,
            negact_fecha datetime NOT NULL,
            negact_fecha_vencimiento datetime NULL,
            negact_completada bit NULL,
            negact_anulada bit NULL,
            negact_fecha_resuelto datetime NULL,
            negact_asesor varchar(20) NULL,
            negact_per_codigo varchar(20) NOT NULL,
            negact_descripcion_virtual varchar(500) NULL,
            ConsecutivoActMiG int NULL)
        """;

    private const string CreateUniversal15Database =
        $"IF DB_ID(N'{Universal15}') IS NULL CREATE DATABASE [{Universal15}];";

    private const string CreateExtended16Database =
        $"IF DB_ID(N'{Extended16}') IS NULL CREATE DATABASE [{Extended16}];";

    private const string SetupUniversal15Table = $"""
        IF OBJECT_ID(N'dbo.tbl_opo_negocios_actividades') IS NULL
        BEGIN
        {Universal15Ddl}
        END
        DELETE FROM dbo.tbl_opo_negocios_actividades;
        """;

    private const string SetupExtended16Table = $"""
        IF OBJECT_ID(N'dbo.tbl_opo_negocios_actividades') IS NULL
        BEGIN
        {Extended16Ddl}
        END
        DELETE FROM dbo.tbl_opo_negocios_actividades;
        """;

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

    private const string SelectRawCharsSql =
        "SELECT negact_tipo, negact_resultado FROM dbo.tbl_opo_negocios_actividades WHERE negact_consecutivoP = @id;";

    private const string SelectPerTenantColumnSql =
        "SELECT ConsecutivoActMiG FROM dbo.tbl_opo_negocios_actividades WHERE negact_consecutivoP = @id;";

    private static readonly DateTime Now = new(2026, 8, 21, 10, 30, 0, DateTimeKind.Unspecified);

    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    public static TheoryData<string> Variants => new() { Universal15, Extended16 };

    private readonly SqlServerContainerFixture _fixture;

    public ActivityMappingTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        await EnsureVariantDatabaseAsync(CreateUniversal15Database, Universal15, SetupUniversal15Table)
            .ConfigureAwait(false);
        await EnsureVariantDatabaseAsync(CreateExtended16Database, Extended16, SetupExtended16Table)
            .ConfigureAwait(false);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    // --- Round-trips through the aggregate --------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AScheduledActivity_RoundTripsOnBothVariants(string variant)
    {
        var dueAt = Now.AddDays(1);
        var scheduled = Activity.Schedule(
            1200, 845, ActivityType.Call, Description.Create("call the applicant").Value, dueAt,
            Advisor, Creator).Value;

        var id = await SaveAsync(variant, scheduled).ConfigureAwait(true);
        id.ShouldBeGreaterThan(0, "the legacy identity column generates the id");

        var activity = await ReadAsync(variant, id).ConfigureAwait(true);

        activity.Status.ShouldBe(ActivityStatus.Scheduled);
        activity.DealId.ShouldBe(1200);
        activity.OpportunityId.ShouldBe(845);
        activity.Type.ShouldBe(ActivityType.Call);
        activity.Description!.Value.ShouldBe("call the applicant");
        activity.DueAt.ShouldBe(dueAt);
        // Created() stamps CreatedAt with DateTime.UtcNow, so the round-trip asserts fidelity
        // against the in-memory aggregate instead of a fixed instant, with tolerance for the
        // precision of the legacy datetime column.
        activity.CreatedAt!.Value.ShouldBe(scheduled.CreatedAt!.Value, TimeSpan.FromSeconds(1));
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

        // The chars on disk are the legacy ones, not the domain enum values (DEC-15).
        var (typeChar, outcomeChar) = await ReadRawCharsAsync(variant, id).ConfigureAwait(true);
        typeChar.ShouldBe("1");
        outcomeChar.ShouldBe("6");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task TheOutcomeType_MaterializesOnNoTrackingQueriesToo(string variant)
    {
        var completed = Activity.RegisterCompleted(
            1200, 845, ActivityType.Meeting, Outcome.Create("met the applicant").Value,
            OutcomeType.ForMeeting(MeetingOutcome.Held).Value, dueAt: null, Advisor, Creator, Now).Value;

        var id = await SaveAsync(variant, completed).ConfigureAwait(true);

        var context = CreateContext(variant);
        await using (context.ConfigureAwait(true))
        {
            var activity = await context.Activities.AsNoTracking()
                .SingleAsync(a => a.Id == id).ConfigureAwait(true);

            activity.OutcomeType!.Name.ShouldBe(nameof(MeetingOutcome.Held));
        }
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
    public async Task AStrayOutcomeCode_SurvivesAnUpdateUntouched(string variant)
    {
        // An email row with a stray code: reads discard it, so a later update of the row (what
        // RepositoryBaseEF.Update does) must not sync the discarded null back into the column.
        var id = await InsertRawAsync(variant, type: "2", outcomeType: "6", completed: true)
            .ConfigureAwait(true);

        var context = CreateContext(variant);
        await using (context.ConfigureAwait(true))
        {
            var activity = await context.Activities.SingleAsync(a => a.Id == id).ConfigureAwait(true);
            activity.OutcomeType.ShouldBeNull("reads discard codes on types without a catalogue");

            context.Activities.Update(activity);
            await context.SaveChangesAsync().ConfigureAwait(true);
        }

        var (_, outcomeChar) = await ReadRawCharsAsync(variant, id).ConfigureAwait(true);
        outcomeChar.ShouldBe("6", "the service must never destroy codes it does not interpret");
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

    // --- Drift and unknown codes fail loudly ------------------------------------------------

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AnUnknownTypeChar_FailsExplicitly_NamingTheColumn(string variant)
    {
        var id = await InsertRawAsync(variant, type: "9", outcomeType: null, completed: null)
            .ConfigureAwait(true);

        var context = CreateContext(variant);
        await using (context.ConfigureAwait(true))
        {
            var exception = await Record
                .ExceptionAsync(() => context.Activities.SingleAsync(a => a.Id == id))
                .ConfigureAwait(true);

            ShouldFailExplicitly(exception, "negact_tipo");
        }
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public async Task AnUnknownOutcomeChar_FailsExplicitly_NamingTheColumn(string variant)
    {
        // '4' is the hole of the legacy call catalogue.
        var id = await InsertRawAsync(variant, type: "1", outcomeType: "4", completed: true)
            .ConfigureAwait(true);

        var context = CreateContext(variant);
        await using (context.ConfigureAwait(true))
        {
            var exception = await Record
                .ExceptionAsync(() => context.Activities.SingleAsync(a => a.Id == id))
                .ConfigureAwait(true);

            ShouldFailExplicitly(exception, "negact_resultado");
        }
    }

    [Fact]
    public async Task ThePerTenantColumn_IsNeverTouched()
    {
        // The insert itself proves EF sends no value for ConsecutivoActMiG; this pins it.
        var completed = Activity.RegisterCompleted(
            1200, 845, ActivityType.Note, Outcome.Create("noted").Value, outcomeType: null,
            dueAt: null, Advisor, Creator, Now).Value;

        var id = await SaveAsync(Extended16, completed).ConfigureAwait(true);

        var connection = new SqlConnection(VariantConnectionString(Extended16));
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

    private static void ShouldFailExplicitly(Exception? exception, string expectedColumn)
    {
        exception.ShouldNotBeNull();

        var messages = string.Empty;
        for (var current = exception; current is not null; current = current.InnerException)
        {
            current.ShouldNotBeOfType<KeyNotFoundException>("D20: never a KeyNotFoundException");
            messages += current.Message + " ";
        }

        messages.ShouldContain(expectedColumn);
    }

    private async Task<int> SaveAsync(string variant, Activity activity)
    {
        var context = CreateContext(variant);
        await using (context.ConfigureAwait(false))
        {
            context.Activities.Add(activity);
            await context.SaveChangesAsync().ConfigureAwait(false);
            return activity.Id;
        }
    }

    private async Task<Activity> ReadAsync(string variant, int id)
    {
        var context = CreateContext(variant);
        await using (context.ConfigureAwait(false))
        {
            return await context.Activities.SingleAsync(a => a.Id == id).ConfigureAwait(false);
        }
    }

    private async Task<(string TypeChar, string? OutcomeChar)> ReadRawCharsAsync(string variant, int id)
    {
        var connection = new SqlConnection(VariantConnectionString(variant));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = SelectRawCharsSql;
                command.Parameters.AddWithValue("@id", id);

                var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                await using (reader.ConfigureAwait(false))
                {
                    (await reader.ReadAsync().ConfigureAwait(false)).ShouldBeTrue();
                    var typeChar = reader.GetString(0);
                    var outcomeChar = await reader.IsDBNullAsync(1).ConfigureAwait(false)
                        ? null
                        : reader.GetString(1);
                    return (typeChar, outcomeChar);
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
        string? advisor = "advisor-01")
    {
        var connection = new SqlConnection(VariantConnectionString(variant));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var command = connection.CreateCommand();
            await using (command.ConfigureAwait(false))
            {
                command.CommandText = InsertRawRowSql;
                command.Parameters.AddWithValue("@deal", 1200);
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

    private async Task EnsureVariantDatabaseAsync(string createDatabaseSql, string database, string setupTableSql)
    {
        var master = new SqlConnection(_fixture.ConnectionString);
        await using (master.ConfigureAwait(false))
        {
            await master.OpenAsync().ConfigureAwait(false);
            var createDatabase = master.CreateCommand();
            await using (createDatabase.ConfigureAwait(false))
            {
#pragma warning disable CA2100 // Only the const SQL strings defined above ever reach this method.
                createDatabase.CommandText = createDatabaseSql;
#pragma warning restore CA2100
                await createDatabase.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        var connection = new SqlConnection(VariantConnectionString(database));
        await using (connection.ConfigureAwait(false))
        {
            await connection.OpenAsync().ConfigureAwait(false);
            var setupTable = connection.CreateCommand();
            await using (setupTable.ConfigureAwait(false))
            {
#pragma warning disable CA2100 // Only the const SQL strings defined above ever reach this method.
                setupTable.CommandText = setupTableSql;
#pragma warning restore CA2100
                await setupTable.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
    }

    private string VariantConnectionString(string database) =>
        new SqlConnectionStringBuilder(_fixture.ConnectionString) { InitialCatalog = database }.ConnectionString;

    private ApplicationDbContext CreateContext(string variant)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(VariantConnectionString(variant))
            .Options;

        return new ApplicationDbContext(options);
    }
}
