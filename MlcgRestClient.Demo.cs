using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    private sealed class DemoWorkspaceEnvelope
    {
        public MlcgDemoWorkspace? Workspace { get; set; }
    }

    private sealed class DemoTemplatesEnvelope
    {
        public List<MlcgSessionSummary> Templates { get; set; } = new();
    }

    /// <summary>
    /// Creates an isolated in-memory demo scope without sending ApiToken, shared
    /// HttpClient headers or login cookies. Does not change this client's ApiToken.
    /// Reuse launchId only for a transport retry of this same application launch.
    /// Requires a server with temporary demo workspaces explicitly enabled.
    /// </summary>
    public async Task<MlcgDemoWorkspace> StartDemoWorkspaceAsync(
        string launchId,
        string sampleId,
        string sampleVersion,
        CancellationToken cancellationToken = default)
    {
        ValidateDemoServiceUrl();
        ValidateDemoUuid(launchId, nameof(launchId));
        if (string.IsNullOrWhiteSpace(sampleId)) throw new ArgumentException("A sample identifier is required.", nameof(sampleId));
        if (string.IsNullOrWhiteSpace(sampleVersion)) throw new ArgumentException("A sample version is required.", nameof(sampleVersion));

        // A separate handler is essential: an injected control client can carry
        // default Authorization, x-mlcg-token or Cookie headers and a cookie jar.
        // Do not follow a redirect that could move this bootstrap to another service.
        using var handler = new HttpClientHandler
        {
            UseCookies = false,
            UseDefaultCredentials = false,
            AllowAutoRedirect = false,
        };
        using var bootstrapClient = new HttpClient(handler) { Timeout = _httpClient.Timeout };
        var envelope = await DemoRequestAsync<DemoWorkspaceEnvelope>(bootstrapClient,
            "POST", "/api/demo/workspaces", new { launchId, sampleId, sampleVersion },
            token: null, cancellationToken).ConfigureAwait(false);
        var workspace = envelope.Workspace ?? throw new InvalidOperationException("No demo workspace returned.");
        if (string.IsNullOrWhiteSpace(workspace.Id) || string.IsNullOrWhiteSpace(workspace.ApiToken))
            throw new InvalidOperationException("Demo workspace creation returned no scope or control token.");
        return workspace;
    }

    /// <summary>Lists the read-only server catalog available to the current demo scope.</summary>
    public async Task<IReadOnlyList<MlcgSessionSummary>> GetDemoTemplatesAsync(CancellationToken cancellationToken = default) =>
        (await DemoControlRequestAsync<DemoTemplatesEnvelope>("GET", "/api/demo/templates", cancellationToken: cancellationToken).ConfigureAwait(false)).Templates;

    /// <summary>
    /// Creates an independent live instance. Reuse instanceKey for a transport
    /// retry of the same selection; a new intentional instance needs a new UUID.
    /// </summary>
    public Task<MlcgDemoSessionResult> CreateDemoSessionAsync(
        string workspaceId,
        string templateId,
        string instanceKey,
        CancellationToken cancellationToken = default)
    {
        ValidateDemoUuid(instanceKey, nameof(instanceKey));
        if (string.IsNullOrWhiteSpace(templateId)) throw new ArgumentException("A template identifier is required.", nameof(templateId));
        return DemoControlRequestAsync<MlcgDemoSessionResult>("POST",
            DemoWorkspacePath(workspaceId) + "/sessions", new { templateId, instanceKey }, cancellationToken);
    }

    /// <summary>Renews this controller's lease; use the server-provided heartbeat interval.</summary>
    public async Task<MlcgDemoWorkspace> HeartbeatDemoWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default) =>
        (await DemoControlRequestAsync<DemoWorkspaceEnvelope>("POST", DemoWorkspacePath(workspaceId) + "/heartbeat", new { }, cancellationToken).ConfigureAwait(false))
        .Workspace ?? throw new InvalidOperationException("No demo workspace returned.");

    /// <summary>Releases this controller. Connected read-only outputs may remain alive until the server's expiry limits.</summary>
    public async Task ReleaseDemoWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        await DemoControlRequestAsync<JsonElement>("POST", DemoWorkspacePath(workspaceId) + "/release", new { }, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Explicitly ends this demo scope, including all instances and their read-only outputs.</summary>
    public async Task EndDemoWorkspaceAsync(string workspaceId, CancellationToken cancellationToken = default)
    {
        await DemoControlRequestAsync<JsonElement>("DELETE", DemoWorkspacePath(workspaceId), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private Task<T> DemoControlRequestAsync<T>(string method, string path, object? body = null, CancellationToken cancellationToken = default)
    {
        ValidateDemoServiceUrl();
        var token = ApiToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Set ApiToken to this demo workspace's control token first.");
        return DemoRequestAsync<T>(_httpClient, method, path, body, token, cancellationToken);
    }

    private Task<T> DemoRequestAsync<T>(HttpClient transport, string method, string path,
        object? body, string? token, CancellationToken cancellationToken)
        => SendJsonAsync<T>(transport, CreateRequest(new HttpMethod(method), path, token, body, PlayoutJson,
            suppressInheritedAuth: ReferenceEquals(transport, _httpClient)), PlayoutJson, cancellationToken);

    private static string DemoWorkspacePath(string workspaceId)
    {
        if (workspaceId == null || !Regex.IsMatch(workspaceId, "\\A[a-zA-Z0-9_-]{1,150}\\z"))
            throw new ArgumentException("A valid demo workspace identifier is required.", nameof(workspaceId));
        return "/api/demo/workspaces/" + Uri.EscapeDataString(workspaceId);
    }

    private static void ValidateDemoUuid(string value, string parameterName)
    {
        if (!Guid.TryParse(value, out var id) || id == Guid.Empty)
            throw new ArgumentException("Use a non-empty UUID generated for this operation.", parameterName);
    }

    private void ValidateDemoServiceUrl()
    {
        if ((_baseUri.Scheme != "http" && _baseUri.Scheme != "https") || _baseUri.UserInfo.Length != 0 || _baseUri.Query.Length != 0 || _baseUri.Fragment.Length != 0)
            throw new InvalidOperationException("Demo connections require an HTTP(S) service URL without credentials, query or fragment.");
    }
}
