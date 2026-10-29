using System.Net.Http;
using System.Text.Json;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
    private Uri _baseUri = new("http://127.0.0.1:3101/");

    public MlcgRestClient(string baseUrl)
        : this(baseUrl, new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }), ownsHttpClient: true)
    {
    }

    /// <summary>Uses a caller-owned transport without changing its headers or disposing it.</summary>
    /// <param name="baseUrl">Absolute HTTP(S) REST service base.</param>
    /// <param name="httpClient">Configure its handler with AllowAutoRedirect=false before constructing the HttpClient. A wrapper cannot change an injected handler's redirect or cookie policy.</param>
    public MlcgRestClient(string baseUrl, HttpClient httpClient)
        : this(baseUrl, httpClient, ownsHttpClient: false)
    {
    }

    private MlcgRestClient(string baseUrl, HttpClient httpClient, bool ownsHttpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
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
        var payload = await SendJsonAsync<BindingsEnvelope>(_httpClient, CreateRequest(
            new HttpMethod("PATCH"), $"/api/sessions/{Uri.EscapeDataString(sessionId)}/bindings", ApiToken?.Trim(),
            new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["values"] = values,
            },
            _jsonOptions), _jsonOptions, cancellationToken).ConfigureAwait(false);
        return payload.Bindings;
    }

    public async Task<IReadOnlyList<MlcgBinding>> ReplaceBindingsAsync(
        string sessionId,
        IEnumerable<MlcgBinding> bindings,
        CancellationToken cancellationToken = default)
    {
        var payload = await SendJsonAsync<BindingsEnvelope>(_httpClient, CreateRequest(
            HttpMethod.Put, $"/api/sessions/{Uri.EscapeDataString(sessionId)}/bindings", ApiToken?.Trim(),
            new Dictionary<string, IEnumerable<MlcgBinding>>
            {
                ["bindings"] = bindings,
            },
            _jsonOptions), _jsonOptions, cancellationToken).ConfigureAwait(false);
        return payload.Bindings;
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

        await SendNoContentAsync(_httpClient, CreateRequest(HttpMethod.Post,
            $"/api/sessions/{Uri.EscapeDataString(sessionId)}/timeline", ApiToken?.Trim(), payload, _jsonOptions),
            cancellationToken).ConfigureAwait(false);
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

        if (!Uri.TryCreate(trimmedOutputBaseUrl, UriKind.Absolute, out var outputUri) ||
            (outputUri.Scheme != "http" && outputUri.Scheme != "https") ||
            outputUri.UserInfo.Length != 0 || outputUri.Query.Length != 0 || outputUri.Fragment.Length != 0 ||
            (_baseUri.Scheme != "http" && _baseUri.Scheme != "https") ||
            _baseUri.UserInfo.Length != 0 || _baseUri.Query.Length != 0 || _baseUri.Fragment.Length != 0)
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

    private Task<T> GetAsync<T>(string path, CancellationToken cancellationToken)
        => SendJsonAsync<T>(_httpClient, CreateRequest(HttpMethod.Get, path, ApiToken?.Trim(), null, _jsonOptions), _jsonOptions, cancellationToken);

    private Uri BuildUri(string path)
    {
        var service = _baseUri;
        if ((service.Scheme != "http" && service.Scheme != "https") || service.UserInfo.Length != 0 || service.Query.Length != 0 || service.Fragment.Length != 0)
            throw new InvalidOperationException("REST connections require an HTTP(S) service URL without credentials, query or fragment.");
        return new Uri(service, path.TrimStart('/'));
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
