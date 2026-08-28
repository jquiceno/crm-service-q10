using System.ComponentModel;

namespace Activities.Application.UseCases.CreateActivity;

/// <summary>
/// Body of <c>POST /activities</c> (§6.2).
/// </summary>
/// <remarks>
/// Every field except <c>DealId</c> is nullable so a missing one is reported by name as a
/// validation error, instead of the deserializer failing with a generic 400.
/// <para>
/// <see cref="ActivityDate"/> is accepted and deliberately not persisted as the activity's
/// creation date: the legacy stored procedure ignored the client's value too, and the real
/// <c>CreatedAt</c> comes from the service's clock (§6.2, DEC-12). It stays in the contract
/// because the monolith adapter still sends it.
/// </para>
/// </remarks>
public sealed record CreateActivityInputDto(
    [property: Description("Consecutive of the deal the activity belongs to.")]
    int DealId,
    [property: Description("Either 'scheduled' or 'completed'.")]
    string? Status,
    [property: Description("One of 'call', 'whatsapp', 'email', 'note' or 'meeting'.")]
    string? Type,
    [property: Description("Identification number of the advisor responsible for the activity.")]
    string? AdvisorIdentification,
    [property: Description("Date the caller reports for the activity. Kept for legacy parity; the stored creation date comes from the service clock.")]
    DateTime? ActivityDate,
    [property: Description("What is planned. Required when scheduled, not allowed when completed.")]
    string? Description,
    [property: Description("What happened. Required when completed, not allowed when scheduled.")]
    string? Outcome,
    [property: Description("Coded outcome, e.g. 'contacted' or 'deal-closed'. Required for a completed call or meeting.")]
    string? OutcomeType,
    [property: Description("When the activity is due. Required when scheduled.")]
    DateTime? DueAt);
