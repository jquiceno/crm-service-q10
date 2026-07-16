namespace Infrastructure.Observability;

/// <summary>
/// HTTP header names related to distributed tracing.
/// </summary>
public static class TraceHeaders
{
    /// <summary>
    /// Response header that exposes the current <see cref="System.Diagnostics.Activity"/>
    /// TraceId (32 hex) for correlation by clients and support.
    /// It is not the W3C <c>traceparent</c> propagation header; that one is handled natively
    /// on service-to-service requests.
    /// </summary>
    public const string TraceId = "X-Trace-Id";
}
