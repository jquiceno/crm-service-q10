namespace Activities.Domain.Enums;

/// <summary>
/// Lifecycle state of an activity.
/// </summary>
/// <remarks>
/// Collapses the two legacy nullable bits <c>negact_completada</c> and <c>negact_anulada</c>
/// into a single state, which makes the invalid combinations unrepresentable (DEC-6).
/// The read precedence — <c>anulada</c> wins over <c>completada</c> — lives in the persistence
/// configuration, because annulling in the legacy writes both bits to 1.
/// </remarks>
public enum ActivityStatus
{
    Scheduled = 1,
    Completed = 2,
    Cancelled = 3
}
