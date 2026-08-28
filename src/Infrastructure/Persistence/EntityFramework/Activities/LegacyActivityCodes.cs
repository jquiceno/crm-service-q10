using Activities.Domain.Enums;
using Activities.Domain.ValueObjects;

namespace Infrastructure.Persistence.EntityFramework.Activities;

/// <summary>
/// The single home of the legacy chars of <c>tbl_opo_negocios_actividades</c> (DEC-15): no other
/// code — domain, application or API — may reference them. An unknown value read from a tenant
/// database fails with an explicit error naming the column and the value, never a
/// <c>KeyNotFoundException</c> (corrects D20): with 378 databases evolving separately, a cryptic
/// failure would hide real schema/data drift.
/// </summary>
/// <remarks>
/// Sources: <c>TIPOS_ACTIVIDADES</c>, <c>EstadoLlamada</c> and <c>EstadoReunion</c> in the Jack
/// monolith. The call catalogue has a hole at <c>'4'</c> (a member commented out years ago) —
/// it must never be reused.
/// </remarks>
internal static class LegacyActivityCodes
{
    /// <summary>
    /// Collapses the legacy bit pair (<c>negact_completada</c>, <c>negact_anulada</c>) into one
    /// status, reading NULL as false — the permanent read convention of DEC-6. A cancelled bit
    /// wins over a completed one.
    /// </summary>
    internal static ActivityStatus ToStatus(bool? isCompleted, bool? isCancelled) =>
        isCancelled == true ? ActivityStatus.Cancelled
        : isCompleted == true ? ActivityStatus.Completed
        : ActivityStatus.Scheduled;

    /// <summary>
    /// Decomposes a status into the legacy bit pair. Always real booleans, never NULL: the NULL
    /// pattern belongs to migrated history (0 rows in the 605 real databases) and new writes
    /// must match what production data looks like. Annulling sets BOTH bits — the monolith
    /// writes <c>completada = 1</c> when it annuls and classifies pending/completed exclusively
    /// by that bit, so a cancelled row with <c>(0,1)</c> would show up as "Programada" in Jack.
    /// </summary>
    internal static (bool IsCompleted, bool IsCancelled) ToStatusBits(ActivityStatus status) =>
        (status is ActivityStatus.Completed or ActivityStatus.Cancelled,
            status == ActivityStatus.Cancelled);

    internal static string ToTypeCode(ActivityType type) => type switch
    {
        ActivityType.Call => "1",
        ActivityType.Email => "2",
        ActivityType.LegacyMeeting => "3",
        ActivityType.Note => "4",
        ActivityType.Meeting => "5",
        ActivityType.VirtualMeeting => "6",
        ActivityType.WhatsApp => "7",
        _ => throw UnknownDomainValue(nameof(ActivityType), type),
    };

    internal static ActivityType ToType(string code) => code switch
    {
        "1" => ActivityType.Call,
        "2" => ActivityType.Email,
        "3" => ActivityType.LegacyMeeting,
        "4" => ActivityType.Note,
        "5" => ActivityType.Meeting,
        "6" => ActivityType.VirtualMeeting,
        "7" => ActivityType.WhatsApp,
        _ => throw UnknownColumnValue("negact_tipo", code),
    };

    internal static string? ToOutcomeTypeCode(OutcomeType? outcomeType) => outcomeType switch
    {
        null => null,
        { Scope: ActivityType.Call } => ToCallOutcomeCode(outcomeType.Name),
        { Scope: ActivityType.Meeting } => ToMeetingOutcomeCode(outcomeType.Name),
        _ => throw UnknownDomainValue(nameof(OutcomeType), outcomeType.Scope),
    };

    /// <summary>
    /// Resolves <c>negact_resultado</c>, whose meaning depends on the activity type: <c>'3'</c>
    /// is a wrong number for a call but a closed deal for a meeting. Every meeting flavour —
    /// <c>'3'</c>, <c>'5'</c> and the virtual <c>'6'</c> — shares the meeting catalogue, which
    /// is how the monolith reads the column (its view model resolves type <c>'1'</c> with
    /// <c>EstadoLlamada</c> and everything else with <c>EstadoReunion</c>, and its completion
    /// modal offers the meeting outcomes for <c>'5'</c> and <c>'6'</c>).
    /// </summary>
    internal static OutcomeType? ToOutcomeType(ActivityType type, string? code)
    {
        if (code is null)
            return null;

        // Legacy parity: a stray code on a row whose type has no catalogue (email, note,
        // WhatsApp) is noise the legacy never interpreted, so reads discard it.
        return type switch
        {
            ActivityType.Call => OutcomeType.ForCall(ToCallOutcome(code)).Value,
            ActivityType.Meeting or ActivityType.LegacyMeeting or ActivityType.VirtualMeeting =>
                OutcomeType.ForMeeting(ToMeetingOutcome(code)).Value,
            _ => null,
        };
    }

    private static string ToCallOutcomeCode(string name) => name switch
    {
        nameof(CallOutcome.NoAnswer) => "1",
        nameof(CallOutcome.Busy) => "2",
        nameof(CallOutcome.WrongNumber) => "3",
        // '4' is the hole of the legacy catalogue — never reuse it.
        nameof(CallOutcome.Voicemail) => "5",
        nameof(CallOutcome.Contacted) => "6",
        nameof(CallOutcome.DealClosed) => "7",
        _ => throw UnknownDomainValue(nameof(CallOutcome), name),
    };

    private static CallOutcome ToCallOutcome(string code) => code switch
    {
        "1" => CallOutcome.NoAnswer,
        "2" => CallOutcome.Busy,
        "3" => CallOutcome.WrongNumber,
        "5" => CallOutcome.Voicemail,
        "6" => CallOutcome.Contacted,
        "7" => CallOutcome.DealClosed,
        _ => throw UnknownColumnValue("negact_resultado", code),
    };

    private static string ToMeetingOutcomeCode(string name) => name switch
    {
        nameof(MeetingOutcome.Held) => "1",
        nameof(MeetingOutcome.Cancelled) => "2",
        nameof(MeetingOutcome.DealClosed) => "3",
        _ => throw UnknownDomainValue(nameof(MeetingOutcome), name),
    };

    private static MeetingOutcome ToMeetingOutcome(string code) => code switch
    {
        "1" => MeetingOutcome.Held,
        "2" => MeetingOutcome.Cancelled,
        "3" => MeetingOutcome.DealClosed,
        _ => throw UnknownColumnValue("negact_resultado", code),
    };

    private static InvalidOperationException UnknownColumnValue(string column, string value) =>
        new($"Unknown legacy code '{value}' in tbl_opo_negocios_actividades.{column}: this tenant " +
            "database holds a value the service does not recognize (schema/data drift). Refusing " +
            "to guess (D20).");

    private static InvalidOperationException UnknownDomainValue(string what, object value) =>
        new($"No legacy code is defined for {what} '{value}'.");
}
