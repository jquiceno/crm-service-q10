using Activities.Domain.Aggregates;
using Activities.Domain.Enums;
using Activities.Domain.ValueObjects;
using Infrastructure.Persistence.EntityFramework.Activities.Entities;
using Infrastructure.Persistence.EntityFramework.Activities.Mappers;
using Shouldly;
using Xunit;

namespace UnitTests.Infrastructure.Activities;

public sealed class ActivityRepositoryMapperTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 10, 30, 0, DateTimeKind.Utc);

    private static PersonCode Advisor => PersonCode.Create("advisor-01").Value;
    private static PersonCode Creator => PersonCode.Create("creator-01").Value;

    [Fact]
    public void ToEntity_FromACompletedCall_WritesTheRawLegacyColumns()
    {
        var aggregate = Activity.RegisterCompleted(
            1200, 845, ActivityType.Call, Outcome.Create("answered").Value,
            OutcomeType.ForCall(CallOutcome.Contacted).Value, dueAt: null,
            Advisor, Creator, Now).Value;

        var entity = ActivityRepositoryMapper.ToEntity(aggregate);

        entity.Type.ShouldBe("1");
        entity.OutcomeCode.ShouldBe("6");
        entity.IsCompleted.ShouldBe(true);
        entity.IsCancelled.ShouldBe(false);
        entity.OutcomeText.ShouldBe("answered");
        entity.Title.ShouldBeNull();
        entity.CompletedAt.ShouldBe(Now);
        entity.AdvisorId.ShouldBe("advisor-01");
        entity.CreatedById.ShouldBe("creator-01");
    }

    [Fact]
    public void ToDomain_RoundTripsWhatToEntityWrites()
    {
        var aggregate = Activity.Schedule(
            1200, 845, ActivityType.Meeting, Description.Create("meet the applicant").Value,
            Now.AddDays(1), Advisor, Creator).Value;

        var entity = ActivityRepositoryMapper.ToEntity(aggregate);
        entity.Id = 42;
        var rebuilt = ActivityRepositoryMapper.ToDomain(entity);

        rebuilt.Id.ShouldBe(42);
        rebuilt.DealId.ShouldBe(1200);
        rebuilt.OpportunityId.ShouldBe(845);
        rebuilt.Type.ShouldBe(ActivityType.Meeting);
        rebuilt.Status.ShouldBe(ActivityStatus.Scheduled);
        rebuilt.Description.ShouldBe(aggregate.Description);
        rebuilt.DueAt.ShouldBe(aggregate.DueAt);
        rebuilt.CreatedAt.ShouldBe(aggregate.CreatedAt);
        rebuilt.AdvisorId.ShouldBe(Advisor);
        rebuilt.CreatedById.ShouldBe(Creator);
        rebuilt.Outcome.ShouldBeNull();
        rebuilt.OutcomeType.ShouldBeNull();
        rebuilt.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void ToDomain_OfAMigratedHistoricRow_ReadsScheduledWithNulls()
    {
        var entity = new ActivityEntity
        {
            Id = 7,
            DealId = 1200,
            Type = "3",
            CreatedAt = Now,
            CreatedById = "legacy",
        };

        var activity = ActivityRepositoryMapper.ToDomain(entity);

        activity.Status.ShouldBe(ActivityStatus.Scheduled, "NULL bits read as not completed / not cancelled");
        activity.Type.ShouldBe(ActivityType.LegacyMeeting);
        activity.Description.ShouldBeNull();
        activity.OutcomeType.ShouldBeNull();
        activity.AdvisorId.ShouldBeNull();
    }

    [Fact]
    public void ToEntity_AfterToDomain_IsLossyByDesign_SoUpdatesMustNeverCopyItBlindly()
    {
        // The domain does not carry stray outcome codes, historic NULL bits, nor the identity,
        // so the repository (F2.4) must copy changed columns selectively on updates: a blanket
        // ToEntity copy over a tracked row would erase/normalize legacy data (DEC-6).
        var strayCodeRow = new ActivityEntity
        {
            Id = 7,
            DealId = 1200,
            Type = "2",
            OutcomeCode = "6",
            IsCompleted = null,
            IsCancelled = null,
            CreatedAt = Now,
            CreatedById = "legacy",
        };

        var copy = ActivityRepositoryMapper.ToEntity(ActivityRepositoryMapper.ToDomain(strayCodeRow));

        copy.Id.ShouldBe(0, "ToEntity builds INSERT rows; the identity never travels back");
        copy.OutcomeCode.ShouldBeNull("a stray code on a type without a catalogue is discarded");
        copy.IsCompleted.ShouldBe(false, "historic NULL bits come back as real booleans");
        copy.IsCancelled.ShouldBe(false, "historic NULL bits come back as real booleans");
    }

    [Fact]
    public void ToDomain_WithAnUnknownTypeChar_FailsExplicitly()
    {
        var entity = new ActivityEntity { Type = "9", CreatedAt = Now, CreatedById = "x" };

        var exception = Should.Throw<InvalidOperationException>(
            () => ActivityRepositoryMapper.ToDomain(entity));

        exception.Message.ShouldContain("negact_tipo");
        exception.Message.ShouldContain("'9'");
    }

    [Fact]
    public void ToDomain_WithAnUnknownOutcomeChar_FailsExplicitly()
    {
        // '4' is the hole of the legacy call catalogue.
        var entity = new ActivityEntity
        {
            Type = "1",
            OutcomeCode = "4",
            CreatedAt = Now,
            CreatedById = "x",
        };

        var exception = Should.Throw<InvalidOperationException>(
            () => ActivityRepositoryMapper.ToDomain(entity));

        exception.Message.ShouldContain("negact_resultado");
        exception.Message.ShouldContain("'4'");
    }
}
