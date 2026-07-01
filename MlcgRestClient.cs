using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed class MlcgRestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private Uri _baseUri = new("http://127.0.0.1:3101/");

    public MlcgRestClient(string baseUrl)
        : this(baseUrl, new HttpClient(), ownsHttpClient: true)
    {
    }

    public MlcgRestClient(string baseUrl, HttpClient httpClient)
        : this(baseUrl, httpClient, ownsHttpClient: false)
    {
    }

    private MlcgRestClient(string baseUrl, HttpClient httpClient, bool ownsHttpClient)
    {
        _httpClient = httpClient;
        _ownsHttpClient = ownsHttpClient;
        BaseUrl = baseUrl;
    }

    public string BaseUrl
    {
        get => _baseUri.ToString().TrimEnd('/');
        set => _baseUri = CreateBaseUri(value);
    }

    public string ApiToken { get; set; } = string.Empty;

    public Task<MlcgHealth> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        return GetAsync<MlcgHealth>("/api/health", cancellationToken);
    }

    public async Task<IReadOnlyList<MlcgSessionSummary>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        var payload = await GetAsync<SessionListEnvelope>("/api/sessions", cancellationToken);
        return payload.Sessions.Count > 0 ? payload.Sessions : payload.Summaries;
    }

    public async Task<IReadOnlyList<MlcgBinding>> GetBindingsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetAsync<BindingsEnvelope>(
            $"/api/sessions/{Uri.EscapeDataString(sessionId)}/bindings",
            cancellationToken);

        return payload.Bindings;
    }

    public async Task<IReadOnlyList<MlcgElement>> GetElementsAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        var payload = await GetAsync<ElementsEnvelope>(
            $"/api/sessions/{Uri.EscapeDataString(sessionId)}/elements",
            cancellationToken);

        return payload.Elements
            .OrderBy(element => element.Depth)
            .ThenBy(element => element.Index)
            .ToArray();
    }

    public Task UpdateBindingAsync(
        string sessionId,
        string key,
        string value,
        CancellationToken cancellationToken = default)
    {
        return UpdateBindingsAsync(sessionId, new Dictionary<string, string>
        {
            [key] = value,
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<MlcgBinding>> UpdateBindingsAsync(
        string sessionId,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();
        var response = await _httpClient.PatchAsJsonAsync(
            BuildUri($"/api/sessions/{Uri.EscapeDataString(sessionId)}/bindings"),
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["values"] = values,
            },
            _jsonOptions,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<BindingsEnvelope>(_jsonOptions, cancellationToken);
        return payload?.Bindings ?? [];
    }

    public async Task<IReadOnlyList<MlcgBinding>> ReplaceBindingsAsync(
        string sessionId,
        IEnumerable<MlcgBinding> bindings,
        CancellationToken cancellationToken = default)
    {
        ApplyAuthHeader();
        var response = await _httpClient.PutAsJsonAsync(
            BuildUri($"/api/sessions/{Uri.EscapeDataString(sessionId)}/bindings"),
            new Dictionary<string, IEnumerable<MlcgBinding>>
            {
                ["bindings"] = bindings,
            },
            _jsonOptions,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<BindingsEnvelope>(_jsonOptions, cancellationToken);
        return payload?.Bindings ?? [];
    }

    public async Task SendTimelineCommandAsync(
        string sessionId,
        string command,
        int? positionMs = null,
        bool? loop = null,
        int? loopCount = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["command"] = command,
        };

        if (positionMs.HasValue)
        {
            payload["positionMs"] = positionMs.Value;
        }

        if (loop.HasValue)
        {
            payload["loop"] = loop.Value;
        }

        if (loopCount.HasValue)
        {
            payload["loopCount"] = loopCount.Value;
        }

        ApplyAuthHeader();
        var response = await _httpClient.PostAsJsonAsync(
            BuildUri($"/api/sessions/{Uri.EscapeDataString(sessionId)}/timeline"),
            payload,
            _jsonOptions,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
    }

    public string BuildOutputUrl(
        string outputBaseUrl,
        string sessionId,
        string background = "frame",
        bool autoplay = true,
        bool loop = true)
    {
        if (string.IsNullOrWhiteSpace(outputBaseUrl))
        {
            return string.Empty;
        }

        var trimmedOutputBaseUrl = outputBaseUrl.Trim().TrimEnd('/');

        if (!Uri.TryCreate(trimmedOutputBaseUrl, UriKind.Absolute, out _))
        {
            return string.Empty;
        }

        var encodedSessionId = Uri.EscapeDataString(sessionId);
        var encodedRestUrl = Uri.EscapeDataString(BaseUrl);
        var autoplayValue = autoplay ? "1" : "0";
        var loopValue = loop ? "1" : "0";
        var encodedBackground = Uri.EscapeDataString(background);

        return $"{trimmedOutputBaseUrl}/#/output/{encodedSessionId}?autoplay={autoplayValue}&loop={loopValue}&bg={encodedBackground}&broker={encodedRestUrl}&rest={encodedRestUrl}&session={encodedSessionId}";
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    private async Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
    {
        ApplyAuthHeader();
        using var response = await _httpClient.GetAsync(BuildUri(path), cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var payload = await response.Content.ReadFromJsonAsync<T>(_jsonOptions, cancellationToken);
        return payload ?? throw new InvalidOperationException($"No JSON payload returned for {path}.");
    }

    private Uri BuildUri(string path)
    {
        return new Uri(_baseUri, path.TrimStart('/'));
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = response.Content == null
            ? string.Empty
            : await response.Content.ReadAsStringAsync();
        var requestUri = response.RequestMessage?.RequestUri?.ToString() ?? "REST request";
        var message = $"{(int)response.StatusCode} {response.ReasonPhrase}: {requestUri}";

        if (!string.IsNullOrWhiteSpace(body))
        {
            message += $" | {body}";
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound &&
            requestUri.IndexOf("/timeline", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            message += " | Timeline control requires an MLCG REST server build with POST /api/sessions/{id}/timeline and a valid selected session id.";
        }

        throw new HttpRequestException(message);
    }

    private void ApplyAuthHeader()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;
        _httpClient.DefaultRequestHeaders.Remove("x-mlcg-token");

        if (string.IsNullOrWhiteSpace(ApiToken))
        {
            return;
        }

        var token = ApiToken.Trim();
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        _httpClient.DefaultRequestHeaders.Add("x-mlcg-token", token);
    }

    private static Uri CreateBaseUri(string baseUrl)
    {
        var trimmedUrl = baseUrl.Trim().TrimEnd('/') + "/";

        if (!Uri.TryCreate(trimmedUrl, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException("REST server URL is invalid.");
        }

        return uri;
    }
}
