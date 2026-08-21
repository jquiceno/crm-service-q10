namespace Activities.Domain.Enums;

/// <summary>
/// Kind of commercial interaction recorded by an activity.
/// </summary>
/// <remarks>
/// The numeric values do NOT match the legacy <c>negact_tipo</c> chars on purpose: the char
/// mapping lives only in the persistence value converter, so no caller can cast this enum to
/// a legacy code (DEC-15).
/// <para>
/// <see cref="VirtualMeeting"/> and <see cref="LegacyMeeting"/> are read-only: historical rows
/// are returned with their real type, but the service never writes them (DEC-5).
/// </para>
/// </remarks>
public enum ActivityType
{
    Call = 1,
    WhatsApp = 2,
    Email = 3,
    Note = 4,
    Meeting = 5,
    VirtualMeeting = 6,
    LegacyMeeting = 7
}
