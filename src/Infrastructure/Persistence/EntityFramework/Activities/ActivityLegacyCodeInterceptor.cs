using Activities.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// Covers the two directions of the legacy shapes a value converter cannot (a converter sees a
/// single column — DEC-15): the (<c>negact_completada</c>, <c>negact_anulada</c>) bit pair that
/// collapses into <see cref="Activity.Status"/> (NULL ⇒ false — DEC-6), and the
/// <c>negact_resultado</c> char whose meaning depends on <c>negact_tipo</c>. Before saving it
/// derives the shadow properties from the domain values, and on materialization it resolves them
/// back — failing explicitly on an unknown code instead of guessing (D20).
/// </summary>
public sealed class ActivityLegacyCodeInterceptor : IMaterializationInterceptor, ISaveChangesInterceptor
{
    public object InitializedInstance(MaterializationInterceptionData materializationData, object entity)
    {
        if (entity is Activity activity)
        {
            var isCompleted = materializationData.GetPropertyValue<bool?>(
                ActivityConfiguration.IsCompletedProperty);
            var isCancelled = materializationData.GetPropertyValue<bool?>(
                ActivityConfiguration.IsCancelledProperty);
            var code = materializationData.GetPropertyValue<string?>(
                ActivityConfiguration.OutcomeTypeCodeProperty);

            activity.RestoreLegacyState(
                LegacyActivityCodes.ToStatus(isCompleted, isCancelled),
                LegacyActivityCodes.ToOutcomeType(activity.Type, code));
        }

        return entity;
    }

    public InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        SyncLegacyColumns(eventData.Context);
        return result;
    }

    public ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        SyncLegacyColumns(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void SyncLegacyColumns(DbContext? context)
    {
        if (context is null)
            return;

        foreach (var entry in context.ChangeTracker.Entries<Activity>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
                continue;

            SyncStatusBits(entry);

            // Only for the types whose outcome column this service interprets: reads discard
            // stray codes on the rest (email, note, WhatsApp), so syncing the discarded null
            // back on an update would erase legacy data the service never understood.
            if (LegacyActivityCodes.OwnsOutcomeCode(entry.Entity.Type))
            {
                entry.Property<string?>(ActivityConfiguration.OutcomeTypeCodeProperty).CurrentValue =
                    LegacyActivityCodes.ToOutcomeTypeCode(entry.Entity.OutcomeType);
            }
        }
    }

    private static void SyncStatusBits(EntityEntry<Activity> entry)
    {
        var isCompleted = entry.Property<bool?>(ActivityConfiguration.IsCompletedProperty);
        var isCancelled = entry.Property<bool?>(ActivityConfiguration.IsCancelledProperty);

        // New rows always get real booleans (production data has 0 NULLs). On updates the bits
        // are rewritten only when the status actually changed, so historic NULL rows round-trip
        // untouched — DEC-6 forbids normalizing them.
        //
        // Constraint: this guard only works for entities TRACKED by the same context (the normal
        // load→mutate→save flow). On a disconnected Update()/Attach the shadow bits arrive null,
        // ToStatus reads Scheduled, and a real status change would be silently skipped — do not
        // enable disconnected updates without bringing the raw bits back into the aggregate.
        if (entry.State is EntityState.Modified
            && LegacyActivityCodes.ToStatus(isCompleted.CurrentValue, isCancelled.CurrentValue)
                == entry.Entity.Status)
        {
            return;
        }

        (isCompleted.CurrentValue, isCancelled.CurrentValue) =
            LegacyActivityCodes.ToStatusBits(entry.Entity.Status);
    }
}
