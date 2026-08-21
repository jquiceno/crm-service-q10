using System.ComponentModel;

namespace BusinessStatus.Application.UseCases.UpdateBusinessStatus;

/// <summary>
/// Request body of <c>PUT /business-statuses/{id}</c>. Full replacement semantics: every field
/// travels and the use case writes the whole aggregate, so an omitted column is never left behind.
/// <c>Name</c> is nullable on purpose so the structural validator reports the failure against its own
/// property instead of the deserializer failing first, and <c>Percentage</c> stays decimal so the
/// aggregate can answer <c>PercentageMustBeInteger</c> as a domain error rather than model binding
/// rejecting it.
/// </summary>
public sealed record UpdateBusinessStatusInputDto(
    [property: Description("Business status name. Required, maximum 200 characters.")]
    string? Name,
    [property: Description("Progress percentage of the business status. A whole number strictly between 0 and 100: both limits are reserved for the terminal statuses (Lost and Won). On a terminal status the stored percentage is immutable and must be sent back unchanged.")]
    decimal Percentage,
    [property: Description("Stage colour as 6 hexadecimal characters without '#', for example '49ff7c'. Optional: omit it or send null to store no colour.")]
    string? Color = null,
    [property: Description("Whether the business status is active. Defaults to true when omitted.")]
    bool IsActive = true);
