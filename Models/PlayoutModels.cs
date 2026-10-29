using System.Text.Json;
using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgClip
{
    public string Id { get; set; } = "clip-" + Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string? SourceSessionId { get; set; }
    public JsonElement? Template { get; set; }
    public Dictionary<string, string> Data { get; set; } = new();
    public Dictionary<string, JsonElement> Tickers { get; set; } = new();
    public long StartMs { get; set; }
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public long? DurationMs { get; set; } = 10000;
}

public sealed class MlcgTimeline
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Untitled timeline";
    public long Revision { get; set; }
    public string Mode { get; set; } = "timeline";
    public bool Loop { get; set; }
    public bool KeepAlive { get; set; }
    public List<MlcgClip> Clips { get; set; } = new();
}

public sealed class MlcgTimelineInstance
{
    public string Id { get; set; } = "instance-" + Guid.NewGuid().ToString("N");
    public string TimelineId { get; set; } = "";
    public long OffsetMs { get; set; }
    public int ZIndex { get; set; }
    public string OutputId { get; set; } = "program";
    public bool Enabled { get; set; } = true;
    public MlcgTimeline? Timeline { get; set; }
}

public sealed class MlcgShow
{
    public MlcgShowExample? Example { get; set; }
    public string Id { get; set; } = "";
    public string Name { get; set; } = "Untitled show";
    public long Revision { get; set; }
    public List<MlcgTimelineInstance> Timelines { get; set; } = new();
}

public sealed class MlcgShowExample
{
    public string Key { get; set; } = "";
    public int Version { get; set; }
    public string Description { get; set; } = "";
    public List<string> Steps { get; set; } = new();
}

public sealed class MlcgRunPlan
{
    public string ShowId { get; set; } = "";
    public string Name { get; set; } = "";
    public long ShowRevision { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public double Fps { get; set; }
    public List<MlcgTimelineInstance> Timelines { get; set; } = new();
}

public sealed class MlcgTimelineState
{
    public string InstanceId { get; set; } = "";
    public long PositionMs { get; set; }
    public int Generation { get; set; }
    public string Status { get; set; } = "";
    public Dictionary<string, long> ManualEnds { get; set; } = new();
}

public sealed class MlcgClipData
{
    public Dictionary<string, string> Bindings { get; set; } = new();
    public Dictionary<string, JsonElement> Tickers { get; set; } = new();
}

public sealed class MlcgRun
{
    public string Id { get; set; } = "";
    public string ShowId { get; set; } = "";
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public long PositionMs { get; set; }
    public string Status { get; set; } = "";
    public string? OutputKey { get; set; }
    public MlcgRunPlan Plan { get; set; } = new();
    public List<MlcgTimelineState> TimelineStates { get; set; } = new();
    public List<MlcgActiveClip> ActiveClips { get; set; } = new();
    public Dictionary<string, MlcgClipData> Overrides { get; set; } = new();
    public JsonElement? PendingPlan { get; set; }
}

public sealed class MlcgTimelinePlanPatch
{
    public string InstanceId { get; set; } = "";
    public MlcgTimeline Timeline { get; set; } = new();
}

public sealed class MlcgShowDesignResult
{
    public MlcgShow Show { get; set; } = new();
    public List<MlcgTimeline> Timelines { get; set; } = new();
}

public sealed class MlcgSchedule
{
    public string Id { get; set; } = "";
    public string ShowId { get; set; } = "";
    public string Name { get; set; } = "";
    public long Revision { get; set; }
    public DateTimeOffset StartAt { get; set; }
    public string Status { get; set; } = "";
    public string? RunId { get; set; }
    public string? OutputChannelId { get; set; }
    public string? OutputId { get; set; }
}

public sealed class MlcgShowPackage
{
    public string Schema { get; set; } = "mlcg-show";
    public int Version { get; set; } = 1;
    public MlcgShow Show { get; set; } = new();
    public List<MlcgTimeline> Timelines { get; set; } = new();
}

public sealed class MlcgRunEvent
{
    public string Type { get; set; } = "";
    public MlcgRun? Run { get; set; }
    public List<MlcgTimelineState>? TimelineStates { get; set; }
    public string? Status { get; set; }
    public long Revision { get; set; }
    public long PositionMs { get; set; }
    public List<MlcgActiveClip>? ActiveClips { get; set; }
}

public sealed class MlcgActiveClip
{
    public string ClipRunId { get; set; } = "";
    public string ClipId { get; set; } = "";
    public string InstanceId { get; set; } = "";
    public string Name { get; set; } = "";
    public string OutputId { get; set; } = "";
    public long SceneTimeMs { get; set; }
    public bool IsPlaying { get; set; }
}
