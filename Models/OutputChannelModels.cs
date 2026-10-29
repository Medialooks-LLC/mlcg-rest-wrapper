using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

/// <summary>A persistent destination. Its URL does not change when a different Run is assigned.</summary>
public sealed class MlcgOutputChannel
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Program";
    public long Revision { get; set; }
    public string OutputId { get; set; } = "program";
    public string? RunId { get; set; }
    /// <summary>Read-only capability. Keep private; intentionally rotate it to revoke old links.</summary>
    public string? OutputKey { get; set; }
    public string? OwnerId { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>Channel metadata in a capability response; no control credential is exposed.</summary>
public sealed class MlcgOutputChannelSnapshot
{
    public MlcgOutputChannel Channel { get; set; } = new();
    public MlcgRun? Run { get; set; }
}

public sealed class MlcgOutputChannelEvent
{
    public string Type { get; set; } = "";
    public MlcgOutputChannel? Channel { get; set; }
    public MlcgRun? Run { get; set; }
    public string? ChannelId { get; set; }
    public long ChannelRevision { get; set; }
    public string? RunId { get; set; }
    public long Revision { get; set; }
    public long PositionMs { get; set; }
    public string? Status { get; set; }
    public List<MlcgTimelineState>? TimelineStates { get; set; }
    public List<MlcgActiveClip>? ActiveClips { get; set; }
}

internal sealed class OutputChannelAssignment
{
    // PlayoutJson omits ordinary null properties; clearing must send runId:null.
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? RunId { get; set; }
    public string? OutputId { get; set; }
}
