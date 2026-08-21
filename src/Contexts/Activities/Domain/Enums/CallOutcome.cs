namespace Activities.Domain.Enums;

/// <summary>
/// Outcome catalogue for a completed <see cref="ActivityType.Call"/>.
/// </summary>
/// <remarks>
/// The legacy enum has a hole at char <c>'4'</c> (commented out years ago); the mapping in the
/// persistence converter must never reuse it. <see cref="DealClosed"/> is a normal, writable
/// value in every institution (DEC-7).
/// </remarks>
public enum CallOutcome
{
    NoAnswer = 1,
    Busy = 2,
    WrongNumber = 3,
    Voicemail = 4,
    Contacted = 5,
    DealClosed = 6
}
