using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    private static readonly JsonSerializerOptions PlayoutJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
    private sealed class PlayoutList<T> { public List<T> Items { get; set; } = new(); }
    private static string Segment(string id) => Uri.EscapeDataString(id);

    private async Task<T> PlayoutRequestAsync<T>(string method, string path, object? body = null, long? revision = null, string? requestKey = null, CancellationToken cancellationToken = default)
    {
        var token = ApiToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Set ApiToken before using scheduling endpoints.");
        using var request = new HttpRequestMessage(new HttpMethod(method), BuildUri("/api/v1/" + path));
        // Capture credentials on this request; do not mutate shared default headers.
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (revision.HasValue) request.Headers.TryAddWithoutValidation("If-Match", "\"" + revision.Value + "\"");
        if (requestKey != null) request.Headers.TryAddWithoutValidation("Idempotency-Key", requestKey);
        if (body != null) request.Content = new StringContent(JsonSerializer.Serialize(body, PlayoutJson), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
        var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        return JsonSerializer.Deserialize<T>(json, PlayoutJson) ?? throw new InvalidOperationException("No playout payload returned.");
    }

    public async Task<IReadOnlyList<MlcgShow>> GetShowsAsync(CancellationToken cancellationToken = default) => (await PlayoutRequestAsync<PlayoutList<MlcgShow>>("GET", "shows", cancellationToken: cancellationToken)).Items;
    public Task<JsonElement> RestorePlayoutExamplesAsync(CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("POST", "examples/restore", new { }, cancellationToken: cancellationToken);
    public Task<MlcgShow> GetShowAsync(string showId, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShow>("GET", "shows/" + Segment(showId), cancellationToken: cancellationToken);
    public Task<MlcgShow> CreateShowAsync(MlcgShow show, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShow>("POST", "shows", show, cancellationToken: cancellationToken);
    public Task<MlcgShow> UpdateShowAsync(MlcgShow show, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShow>("PUT", "shows/" + Segment(show.Id), show, show.Revision, cancellationToken: cancellationToken);
    /// <summary>Saves a show and edited timelines atomically. A stale revision leaves every definition unchanged.</summary>
    public Task<MlcgShowDesignResult> SaveShowDesignAsync(MlcgShow show, IEnumerable<MlcgTimeline> editedTimelines, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShowDesignResult>("PUT", "shows/" + Segment(show.Id) + "/design", new { show, timelines = editedTimelines }, show.Revision, cancellationToken: cancellationToken);
    public Task<JsonElement> DeleteShowAsync(string showId, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("DELETE", "shows/" + Segment(showId), revision: revision, cancellationToken: cancellationToken);
    public Task<JsonElement> ValidateShowAsync(string showId, CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("POST", "shows/" + Segment(showId) + "/validate", new { }, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<MlcgTimeline>> GetTimelinesAsync(CancellationToken cancellationToken = default) => (await PlayoutRequestAsync<PlayoutList<MlcgTimeline>>("GET", "timelines", cancellationToken: cancellationToken)).Items;
    public Task<MlcgTimeline> GetTimelineAsync(string timelineId, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("GET", "timelines/" + Segment(timelineId), cancellationToken: cancellationToken);
    public Task<MlcgTimeline> CreateTimelineAsync(MlcgTimeline timeline, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("POST", "timelines", timeline, cancellationToken: cancellationToken);
    public Task<MlcgTimeline> UpdateTimelineAsync(MlcgTimeline timeline, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("PUT", "timelines/" + Segment(timeline.Id), timeline, timeline.Revision, cancellationToken: cancellationToken);
    public Task<JsonElement> DeleteTimelineAsync(string timelineId, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("DELETE", "timelines/" + Segment(timelineId), revision: revision, cancellationToken: cancellationToken);
    public Task<MlcgTimeline> AddClipAsync(string timelineId, MlcgClip clip, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("POST", "timelines/" + Segment(timelineId) + "/clips", clip, revision, cancellationToken: cancellationToken);
    public Task<MlcgTimeline> UpdateClipAsync(string timelineId, MlcgClip clip, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("PATCH", "timelines/" + Segment(timelineId) + "/clips/" + Segment(clip.Id), clip, revision, cancellationToken: cancellationToken);
    public Task<MlcgTimeline> DeleteClipAsync(string timelineId, string clipId, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("DELETE", "timelines/" + Segment(timelineId) + "/clips/" + Segment(clipId), revision: revision, cancellationToken: cancellationToken);
    public Task<MlcgTimeline> UpdateClipDataAsync(string timelineId, string clipId, MlcgClipData data, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgTimeline>("PATCH", "timelines/" + Segment(timelineId) + "/clips/" + Segment(clipId) + "/data", data, revision, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<MlcgRun>> GetRunsAsync(CancellationToken cancellationToken = default) => (await PlayoutRequestAsync<PlayoutList<MlcgRun>>("GET", "runs", cancellationToken: cancellationToken)).Items;
    public Task<MlcgRun> GetRunAsync(string runId, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("GET", "runs/" + Segment(runId), cancellationToken: cancellationToken);
    public Task<MlcgRun> StartShowAsync(string showId, string idempotencyKey, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("POST", "runs", new { showId }, requestKey: idempotencyKey, cancellationToken: cancellationToken);
    public Task<MlcgRun> PrepareShowAsync(string showId, string idempotencyKey, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("POST", "runs", new { showId, startPaused = true }, requestKey: idempotencyKey, cancellationToken: cancellationToken);
    public Task<MlcgRun> SendRunCommandAsync(string runId, string command, long revision, string? instanceId = null, long? positionMs = null, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("POST", "runs/" + Segment(runId) + "/commands", new { command, instanceId, positionMs }, revision, cancellationToken: cancellationToken);
    public Task<MlcgRun> UpdateLiveClipDataAsync(string runId, string clipRunId, MlcgClipData data, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("PATCH", "runs/" + Segment(runId) + "/clips/" + Segment(clipRunId) + "/data", data, revision, cancellationToken: cancellationToken);
    public Task<MlcgRun> UpdateRunPlanAsync(string runId, IEnumerable<MlcgTimelinePlanPatch> timelines, long revision, long? effectiveAtMs = null, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("PATCH", "runs/" + Segment(runId) + "/plan", new { timelines, effectiveAtMs }, revision, cancellationToken: cancellationToken);
    /// <summary>Replaces all live timeline instances, including routing and offsets. Does not modify the saved show.</summary>
    public Task<MlcgRun> ReplaceRunPlanAsync(string runId, IEnumerable<MlcgTimelineInstance> timelines, long revision, long? effectiveAtMs = null, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgRun>("PATCH", "runs/" + Segment(runId) + "/plan", new { replaceInstances = true, timelines = timelines.Select(item => new { instanceId = item.Id, item.TimelineId, item.OffsetMs, item.ZIndex, item.OutputId, item.Enabled, item.Timeline }), effectiveAtMs }, revision, cancellationToken: cancellationToken);
    public async Task<MlcgRun> AddLiveClipAsync(string runId, string instanceId, MlcgClip clip, long? effectiveAtMs = null, CancellationToken cancellationToken = default)
    {
        var run = await GetRunAsync(runId, cancellationToken).ConfigureAwait(false);
        var instance = run.Plan.Timelines.FirstOrDefault(item => item.Id == instanceId) ?? throw new ArgumentException("Timeline instance not found.");
        instance.Timeline!.Clips.Add(clip);
        return await UpdateRunPlanAsync(runId, new[] { new MlcgTimelinePlanPatch { InstanceId = instanceId, Timeline = instance.Timeline } }, run.Revision, effectiveAtMs, cancellationToken).ConfigureAwait(false);
    }
    public Task<JsonElement> DeleteRunAsync(string runId, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("DELETE", "runs/" + Segment(runId), revision: revision, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<MlcgSchedule>> GetSchedulesAsync(CancellationToken cancellationToken = default) => (await PlayoutRequestAsync<PlayoutList<MlcgSchedule>>("GET", "schedules", cancellationToken: cancellationToken)).Items;
    public Task<MlcgSchedule> ScheduleShowAsync(string showId, DateTimeOffset startAt, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgSchedule>("POST", "schedules", new { showId, startAt = startAt.ToUniversalTime().ToString("O") }, cancellationToken: cancellationToken);
    public Task<JsonElement> CancelScheduleAsync(string scheduleId, long revision, CancellationToken cancellationToken = default) => PlayoutRequestAsync<JsonElement>("DELETE", "schedules/" + Segment(scheduleId), revision: revision, cancellationToken: cancellationToken);
    public Task<MlcgShowPackage> ExportShowAsync(string showId, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShowPackage>("GET", "shows/" + Segment(showId) + "/export", cancellationToken: cancellationToken);
    public Task<MlcgShow> ImportShowAsync(MlcgShowPackage package, CancellationToken cancellationToken = default) => PlayoutRequestAsync<MlcgShow>("POST", "import", package, cancellationToken: cancellationToken);

    public string BuildShowOutputUrl(string outputBaseUrl, MlcgRun run, string outputId = "program")
    {
        if (!Uri.TryCreate(outputBaseUrl, UriKind.Absolute, out _) || string.IsNullOrEmpty(run.OutputKey)) throw new ArgumentException("A valid output base and run output key are required.");
        return outputBaseUrl.TrimEnd('/') + "/#/output/run/" + Segment(run.Id) + "?key=" + Segment(run.OutputKey!) + "&broker=" + Segment(BaseUrl) + "&output=" + Segment(outputId);
    }

    public static string BuildClipRunId(string instanceId, string clipId, long cycle = 0, int generation = 0) => instanceId + ":" + clipId + ":" + (generation == 0 ? cycle.ToString() : generation + "-" + cycle);

    /// <summary>Receives run.snapshot followed by run.clock events. Marshal callbacks to your UI thread if needed. Reconnect explicitly after interruption.</summary>
    public async Task SubscribeRunAsync(string runId, Func<MlcgRunEvent, Task> onEvent, CancellationToken cancellationToken = default)
    {
        var token = ApiToken?.Trim();
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("Set ApiToken before subscribing to a run.");
        var builder = new UriBuilder(_baseUri) { Scheme = _baseUri.Scheme == "https" ? "wss" : "ws", Path = "/ws/playout", Query = "run=" + Segment(runId) };
        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", "Bearer " + token);
        await socket.ConnectAsync(builder.Uri, cancellationToken).ConfigureAwait(false);
        var buffer = new byte[8192];
        while (socket.State == WebSocketState.Open)
        {
            using var content = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) return;
                content.Write(buffer, 0, result.Count);
                if (content.Length > 20_000_000) throw new InvalidOperationException("Run event exceeds 20 MB.");
            } while (!result.EndOfMessage);
            var message = JsonSerializer.Deserialize<MlcgRunEvent>(Encoding.UTF8.GetString(content.ToArray()), PlayoutJson);
            if (message != null) await onEvent(message).ConfigureAwait(false);
        }
    }
}
