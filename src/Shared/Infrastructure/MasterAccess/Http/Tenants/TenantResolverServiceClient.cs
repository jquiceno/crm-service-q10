using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Infrastructure.MasterAccess.Security;
using Shared.Application.Caching;
using Shared.Application.Ports;
using Shared.Results;
using Shared.Results.Errors;

namespace Infrastructure.MasterAccess.Http.Tenants;

public sealed class TenantResolverServiceClient(
    HttpClient httpClient,
    ICacheStore cache,
    IConnectionStringDecryptor decryptor,
    IOptions<TenantResolverServiceSettings> options,
    ILoggerPort<TenantResolverServiceClient> logger) : ITenantResolverServiceClient
{
    private const string Context = "TenantInfo";

    private const string SupportedAlgorithm = "aes-256-cbc";

    private readonly TenantResolverServiceSettings _settings = options.Value;

    public async Task<Result<TenantInfo>> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new ValidationError("Tenant code is required.", ErrorType.Validation) { Property = "code" };

        if (code.Contains(':', StringComparison.Ordinal))
            return new ValidationError("Tenant code must not contain ':'.", ErrorType.Validation) { Property = "code" };

        try
        {
            var key = CacheKey.For("masteraccess").Resource("tenant", code);

            var cached = await cache.GetAsync<TenantInfoResponse>(key, cancellationToken).ConfigureAwait(false);
            if (cached is not null)
                return Decrypt(cached);

            using var response = await httpClient
                .GetAsync(Uri.EscapeDataString(code), cancellationToken)
                .ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new NotFoundError($"Tenant with code '{code}' was not found.") { Context = Context };

            if (!response.IsSuccessStatusCode)
            {
                logger.Error(null, "Tenant info endpoint returned {StatusCode} for code {Code}",
                    (int)response.StatusCode, code);
                return new InternalError("The tenant info service returned an error.") { Context = Context };
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<TenantInfoEnvelope>(cancellationToken)
                .ConfigureAwait(false);

            var payload = envelope?.Data;
            if (payload is null || string.IsNullOrWhiteSpace(payload.DbConnectionString))
            {
                logger.Error(null, "Tenant info endpoint returned an empty/invalid payload for code {Code}", code);
                return new InternalError("The tenant info service returned an invalid payload.") { Context = Context };
            }

            // Reject an unexpected algorithm up front (an empty value trusts the AES-256-CBC default).
            if (!string.IsNullOrWhiteSpace(payload.EncryptionAlgorithm)
                && !string.Equals(payload.EncryptionAlgorithm, SupportedAlgorithm, StringComparison.OrdinalIgnoreCase))
            {
                logger.Error(null, "Tenant info endpoint reported unsupported encryption algorithm {Algorithm} for code {Code}",
                    payload.EncryptionAlgorithm, code);
                return new InternalError("The tenant info service used an unsupported encryption algorithm.") { Context = Context };
            }

            var tenant = Decrypt(payload);
            if (tenant.IsFailure)
                return tenant;

            await cache.SetAsync(key, payload, TimeSpan.FromMinutes(_settings.CacheTtlMinutes), cancellationToken)
                .ConfigureAwait(false);
            return tenant;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.Error(ex, "Error calling tenant info endpoint for code {Code}", code);
            return new InternalError("A network error occurred while resolving the tenant.") { Context = Context };
        }
    }

    private Result<TenantInfo> Decrypt(TenantInfoResponse raw)
    {
        var connectionString = decryptor.Decrypt(raw.DbConnectionString!);
        if (connectionString.IsFailure)
            return connectionString.Error;

        return new TenantInfo(raw.EntityCode ?? string.Empty, raw.DbName ?? string.Empty, connectionString.Value);
    }
}
