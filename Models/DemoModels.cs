using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

/// <summary>A temporary, isolated demo scope. Its control token is returned only by creation.</summary>
public sealed class MlcgDemoWorkspace
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("apiToken")]
    public string? ApiToken { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("expiresAt")]
    public DateTimeOffset ExpiresAt { get; set; }

    [JsonPropertyName("maximumExpiresAt")]
    public DateTimeOffset MaximumExpiresAt { get; set; }

    [JsonPropertyName("heartbeatIntervalSeconds")]
    public int HeartbeatIntervalSeconds { get; set; }

    [JsonPropertyName("limits")]
    public MlcgDemoLimits Limits { get; set; } = new();

    [JsonPropertyName("usageNotice")]
    public string UsageNotice { get; set; } = string.Empty;
}

/// <summary>Server-enforced limits, not an invitation for a client to allocate that capacity.</summary>
public sealed class MlcgDemoLimits
{
    [JsonPropertyName("maxSessions")]
    public int MaxSessions { get; set; }

    [JsonPropertyName("maxViewersPerSession")]
    public int MaxViewersPerSession { get; set; }

    [JsonPropertyName("maxBindings")]
    public int MaxBindings { get; set; }

    [JsonPropertyName("idleTimeoutSeconds")]
    public int IdleTimeoutSeconds { get; set; }

    [JsonPropertyName("maximumLifetimeSeconds")]
    public int MaximumLifetimeSeconds { get; set; }

    [JsonPropertyName("maxWorkspaces")]
    public int MaxWorkspaces { get; set; }
}

/// <summary>An independent instance of a read-only demo template and its editable values.</summary>
public sealed class MlcgDemoSessionResult
{
    [JsonPropertyName("session")]
    public MlcgSessionSummary Session { get; set; } = new();

    [JsonPropertyName("bindings")]
    public List<MlcgBinding> Bindings { get; set; } = new();
}
