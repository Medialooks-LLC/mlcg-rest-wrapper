using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    // ApiToken and BaseUrl are captured before the first await for each request.
    // An injected HttpClient is shared caller-owned infrastructure: never write
    // its DefaultRequestHeaders or dispose it when this client is disposed.
    private HttpRequestMessage CreateRequest(HttpMethod method, string path, string? token,
        object? body, JsonSerializerOptions options, long? revision = null, string? requestKey = null,
        bool suppressInheritedAuth = true)
    {
        var uri = BuildUri(path);
        var json = body == null ? null : JsonSerializer.Serialize(body, options);
        var request = new HttpRequestMessage(method, uri);
        try
        {
            if (string.IsNullOrWhiteSpace(token) && suppressInheritedAuth)
            {
                // Explicit empty values suppress inherited control credentials
                // without altering the caller's shared default headers.
                request.Headers.TryAddWithoutValidation("Authorization", string.Empty);
                request.Headers.TryAddWithoutValidation("x-mlcg-token", string.Empty);
            }
            else if (!string.IsNullOrWhiteSpace(token))
            {
                if (token.Any(char.IsControl)) throw new ArgumentException("ApiToken contains invalid HTTP header characters.", nameof(ApiToken));
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                request.Headers.TryAddWithoutValidation("x-mlcg-token", token);
            }
            if (revision.HasValue) request.Headers.TryAddWithoutValidation("If-Match", "\"" + revision.Value + "\"");
            if (requestKey != null)
            {
                if (requestKey.Any(char.IsControl)) throw new ArgumentException("The idempotency key contains invalid HTTP header characters.", nameof(requestKey));
                request.Headers.TryAddWithoutValidation("Idempotency-Key", requestKey);
            }
            if (json != null) request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            return request;
        }
        catch
        {
            request.Dispose();
            throw;
        }
    }

    private Task SendSessionRequestAsync(HttpMethod method, string path, CancellationToken cancellationToken)
        => SendNoContentAsync(_httpClient, CreateRequest(method, path, ApiToken?.Trim(), null, _jsonOptions), cancellationToken);

    private async Task<T> SendJsonAsync<T>(HttpClient transport, HttpRequestMessage request,
        JsonSerializerOptions options, CancellationToken cancellationToken)
    {
        using (request)
        using (var deadline = CreateDeadline(transport, cancellationToken))
        {
            try
            {
                using var response = await SendResponseAsync(transport, request, deadline.Token).ConfigureAwait(false);
                EnsureSuccess(response, deadline.Token);
                if (response.Content == null) throw new InvalidOperationException("The MLCG server returned no JSON payload.");
                using var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                var payload = await JsonSerializer.DeserializeAsync<T>(stream, options, deadline.Token).ConfigureAwait(false);
                return payload ?? throw new InvalidOperationException("The MLCG server returned no JSON payload.");
            }
            catch (OperationCanceledException)
            {
                throw CreateCancellationException(cancellationToken);
            }
            catch (JsonException)
            {
                throw new InvalidOperationException("The MLCG server returned an invalid JSON payload.");
            }
            catch (HttpRequestException error) when (error is not MlcgRestException)
            {
                throw new MlcgRestException(null);
            }
            catch (IOException)
            {
                throw new MlcgRestException(null);
            }
        }
    }

    private static async Task SendNoContentAsync(HttpClient transport, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using (request)
        using (var deadline = CreateDeadline(transport, cancellationToken))
        {
            try
            {
                using var response = await SendResponseAsync(transport, request, deadline.Token).ConfigureAwait(false);
                EnsureSuccess(response, deadline.Token);
            }
            catch (OperationCanceledException) { throw CreateCancellationException(cancellationToken); }
        }
    }

    private static CancellationTokenSource CreateDeadline(HttpClient transport, CancellationToken cancellationToken)
    {
        // ResponseHeadersRead stops HttpClient's timer at the headers. Keep the
        // caller's configured timeout active through JSON streaming as well.
        var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (transport.Timeout != Timeout.InfiniteTimeSpan) deadline.CancelAfter(transport.Timeout);
        return deadline;
    }

    private static OperationCanceledException CreateCancellationException(CancellationToken cancellationToken)
        => cancellationToken.IsCancellationRequested
            ? new OperationCanceledException("The MLCG REST request was canceled.", cancellationToken)
            : new TaskCanceledException("The MLCG REST request timed out.");

    private static async Task<HttpResponseMessage> SendResponseAsync(HttpClient transport, HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return await transport.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // Native transport errors may contain a URL or reflected credentials.
            // Do not retain the unsafe exception as InnerException either.
            throw new MlcgRestException(null);
        }
    }

    private static void EnsureSuccess(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (response.IsSuccessStatusCode) return;
        TimeSpan? retryAfter = null;
        var header = response.Headers.RetryAfter;
        var delay = header?.Delta ?? (header?.Date - DateTimeOffset.UtcNow);
        if (delay.HasValue)
            retryAfter = delay.Value < TimeSpan.Zero ? TimeSpan.Zero : delay.Value > TimeSpan.FromHours(24) ? TimeSpan.FromHours(24) : delay.Value;
        // Never read or reflect the error body, ReasonPhrase, or request URL.
        throw new MlcgRestException(response.StatusCode, retryAfter);
    }
}
