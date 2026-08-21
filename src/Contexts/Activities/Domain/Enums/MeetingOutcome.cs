namespace Activities.Domain.Enums;

/// <summary>
/// Outcome catalogue for a completed <see cref="ActivityType.Meeting"/>.
/// </summary>
/// <remarks>
/// <see cref="Cancelled"/> describes how the meeting ended and is unrelated to
/// <see cref="ActivityStatus.Cancelled"/>, which describes the activity being annulled.
/// <see cref="DealClosed"/> is a normal, writable value (DEC-7).
/// </remarks>
public enum MeetingOutcome
{
    Held = 1,
    Cancelled = 2,
    DealClosed = 3
}
