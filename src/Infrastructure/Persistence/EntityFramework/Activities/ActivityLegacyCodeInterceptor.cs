using Activities.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Covers the two directions of the <c>negact_resultado</c> boundary that a value converter
/// cannot (the char's meaning depends on <c>negact_tipo</c> — DEC-15): before saving it derives
/// the char from <see cref="Activity.OutcomeType"/> into the shadow property, and on
/// materialization it resolves the char back into the value object, failing explicitly on an
/// unknown code instead of guessing (D20).
/// </summary>
public sealed class ActivityLegacyCodeInterceptor : IMaterializationInterceptor, ISaveChangesInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Activity activity)
        {
            var code = materializationData.GetPropertyValue<string?>(
                ActivityConfiguration.OutcomeTypeCodeProperty);
            activity.RestoreOutcomeType(LegacyActivityCodes.ToOutcomeType(activity.Type, code));
        }

        return entity;
    }

    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        SyncOutcomeTypeCodes(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SyncOutcomeTypeCodes(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void SyncOutcomeTypeCodes(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<Activity>())
        {
            // Only for the types whose outcome column this service interprets: reads discard
            // stray codes on the rest (email, note, WhatsApp), so syncing the discarded null
            // back on an update would erase legacy data the service never understood.
            if (entry.State is EntityState.Added or EntityState.Modified
                && LegacyActivityCodes.OwnsOutcomeCode(entry.Entity.Type))
            {
                entry.Property<string?>(ActivityConfiguration.OutcomeTypeCodeProperty).CurrentValue =
                    LegacyActivityCodes.ToOutcomeTypeCode(entry.Entity.OutcomeType);
            }
        }
    }
}
