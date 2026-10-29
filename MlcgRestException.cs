using System.Net;
using System.Net.Http;

namespace MLCG.RestControl;

/// <summary>A redacted REST failure. Response bodies, URLs and credentials are never included.</summary>
public sealed class MlcgRestException : HttpRequestException
{
    internal MlcgRestException(HttpStatusCode? statusCode, TimeSpan? retryAfter = null)
#if NETFRAMEWORK
        : base(CreateMessage(statusCode))
#else
        : base(CreateMessage(statusCode), null, statusCode)
#endif
    {
#if NETFRAMEWORK
        StatusCode = statusCode;
#endif
        RetryAfter = retryAfter;
    }

#if NETFRAMEWORK
    /// <summary>The HTTP status, or null when no HTTP response was received.</summary>
    public HttpStatusCode? StatusCode { get; }
#endif

    /// <summary>The server's Retry-After delay, clamped to zero through 24 hours; null if absent or invalid. No request is retried automatically.</summary>
    public TimeSpan? RetryAfter { get; }

    private static string CreateMessage(HttpStatusCode? statusCode) => statusCode.HasValue
        ? ((int)statusCode.Value) + " MLCG REST request failed. Check StatusCode and RetryAfter."
        : "MLCG REST transport failed before an HTTP response was received.";
}
