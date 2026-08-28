using System.Text;
using Activities.Domain.Enums;

namespace Activities.Application.Mapping;

/// <summary>
/// Translates between the kebab-case names of the public contract (§6) and the domain enums.
/// </summary>
/// <remarks>
/// One place for both directions so reading and writing can never drift apart: whatever
/// <see cref="ToContract(ActivityType)"/> renders, <see cref="TryParseType"/> accepts back.
/// <para>
/// The maps are written out instead of derived from the member names because two of them are not
/// mechanical: <see cref="ActivityType.WhatsApp"/> is one word in the contract, and
/// <see cref="ActivityType.LegacyMeeting"/> — the legacy <c>'3'</c> rows — is reported as a plain
/// meeting, which is what it always meant to the user (§6.1). Outcome names are mechanical, so
/// they do go through <see cref="ToKebabCase"/>.
/// </para>
/// </remarks>
public static class ContractNames
{
    public static string ToContract(ActivityType type) => type switch
    {
        ActivityType.Call => "call",
        ActivityType.WhatsApp => "whatsapp",
        ActivityType.Email => "email",
        ActivityType.Note => "note",
        ActivityType.Meeting => "meeting",
        ActivityType.VirtualMeeting => "virtual-meeting",
        ActivityType.LegacyMeeting => "meeting",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown activity type."),
    };

    public static string ToContract(ActivityStatus status) => status switch
    {
        ActivityStatus.Scheduled => "scheduled",
        ActivityStatus.Completed => "completed",
        ActivityStatus.Cancelled => "cancelled",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown activity status."),
    };

    /// <summary>
    /// Resolves a contract type name. <c>virtual-meeting</c> resolves on purpose, so the aggregate
    /// can reject it as not writable instead of the request looking like a typo (§6.2).
    /// </summary>
    public static bool TryParseType(string? name, out ActivityType type)
    {
        type = default;

        switch (Normalize(name))
        {
            case "call": type = ActivityType.Call; return true;
            case "whatsapp": type = ActivityType.WhatsApp; return true;
            case "email": type = ActivityType.Email; return true;
            case "note": type = ActivityType.Note; return true;
            case "meeting": type = ActivityType.Meeting; return true;
            case "virtualmeeting": type = ActivityType.VirtualMeeting; return true;
            default: return false;
        }
    }

    /// <summary>
    /// Resolves a contract status name. <c>cancelled</c> resolves like the rest: rejecting it as a
    /// creation status is the use case's call, not a parsing failure.
    /// </summary>
    public static bool TryParseStatus(string? name, out ActivityStatus status)
    {
        status = default;

        switch (Normalize(name))
        {
            case "scheduled": status = ActivityStatus.Scheduled; return true;
            case "completed": status = ActivityStatus.Completed; return true;
            case "cancelled": status = ActivityStatus.Cancelled; return true;
            default: return false;
        }
    }

    /// <summary>
    /// Turns a contract outcome name into what the domain resolves: <c>deal-closed</c> becomes
    /// <c>dealclosed</c>, which <c>OutcomeType.Create</c> matches against <c>DealClosed</c>
    /// case-insensitively. Returns null for empty input, which the domain reads as "no outcome
    /// type given".
    /// </summary>
    /// <remarks>
    /// Stripping the dashes instead of re-casing the words leans on that case-insensitive match —
    /// which is why <c>OutcomeType.Create</c> must keep it. It also means the catalogue can grow
    /// new members without this method learning about them.
    /// </remarks>
    public static string? ToOutcomeName(string? contractName)
    {
        var normalized = Normalize(contractName);
        return normalized.Length == 0 ? null : normalized;
    }

    /// <summary>Renders an outcome type as the contract spells it: <c>NoAnswer</c> → <c>no-answer</c>.</summary>
    public static string ToOutcomeContract(string outcomeName) => ToKebabCase(outcomeName);

    /// <summary>
    /// Lower-cases and strips the dashes, so <c>Deal-Closed</c> and <c>dealclosed</c> match alike.
    /// </summary>
    /// <remarks>
    /// Deliberately lenient: the strangler sits behind an adapter that has been sending these
    /// names for years, and rejecting <c>CALL</c> over its casing would be a new failure mode the
    /// legacy endpoint never had. It only ever widens what is accepted — a name outside the
    /// catalogue is still rejected.
    /// </remarks>
    private static string Normalize(string? name) =>
        name is null
            ? string.Empty
            : name.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();

    private static string ToKebabCase(string name)
    {
        var kebab = new StringBuilder(name.Length + 4);

        foreach (var character in name)
        {
            if (char.IsUpper(character) && kebab.Length > 0)
                kebab.Append('-');

            kebab.Append(char.ToLowerInvariant(character));
        }

        return kebab.ToString();
    }
}
