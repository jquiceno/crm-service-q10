using System.Text.Json.Serialization;

namespace Infrastructure.MasterAccess.Http.Tenants;

/// <summary>
/// Envelope returned by the NestJS tenant-resolver service: every success is wrapped as
/// <c>{ "data": { ... }, "statusCode": n }</c>. Only <see cref="Data"/> is of interest.
/// </summary>
internal sealed record TenantInfoEnvelope
{
    [JsonPropertyName("data")]
    public TenantInfoResponse? Data { get; init; }
}

internal sealed record TenantInfoResponse
{
    [JsonPropertyName("entityCode")]
    public string? EntityCode { get; init; }

    [JsonPropertyName("dbName")]
    public string? DbName { get; init; }

    [JsonPropertyName("dbConnectionString")]
    public string? DbConnectionString { get; init; }

    [JsonPropertyName("encryptionAlgorithm")]
    public string? EncryptionAlgorithm { get; init; }
}
