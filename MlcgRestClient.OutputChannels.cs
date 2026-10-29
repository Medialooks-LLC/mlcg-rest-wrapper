using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    public async Task<IReadOnlyList<MlcgOutputChannel>> GetOutputChannelsAsync(CancellationToken cancellationToken = default) =>
        (await PlayoutRequestAsync<PlayoutList<MlcgOutputChannel>>("GET", "output-channels", cancellationToken: cancellationToken).ConfigureAwait(false)).Items;

    public Task<MlcgOutputChannel> GetOutputChannelAsync(string channelId, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("GET", "output-channels/" + Segment(channelId), cancellationToken: cancellationToken);

    public Task<MlcgOutputChannel> CreateOutputChannelAsync(string name, string outputId = "program", CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("POST", "output-channels", new { name, outputId }, cancellationToken: cancellationToken);

    /// <summary>Idempotently provisions the account's default Program destination. Does not put a Run on air.</summary>
    public Task<MlcgOutputChannel> EnsureDefaultOutputChannelAsync(CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("POST", "output-channels", new { name = "Program", @default = true }, cancellationToken: cancellationToken);

    public Task<MlcgOutputChannel> RenameOutputChannelAsync(string channelId, string name, long revision, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("PATCH", "output-channels/" + Segment(channelId), new { name }, revision, cancellationToken: cancellationToken);

    /// <summary>An explicit on-air action. Existing viewers follow the new assigned Run without replacing their URL.</summary>
    public Task<MlcgOutputChannel> AssignOutputChannelAsync(string channelId, string runId, long revision, string outputId = "program", CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("PUT", "output-channels/" + Segment(channelId) + "/assignment", new OutputChannelAssignment { RunId = runId, OutputId = outputId }, revision, cancellationToken: cancellationToken);

    /// <summary>Clears the delivered picture, but does not stop the previously assigned Run.</summary>
    public Task<MlcgOutputChannel> ClearOutputChannelAsync(string channelId, long revision, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("PUT", "output-channels/" + Segment(channelId) + "/assignment", new OutputChannelAssignment { RunId = null }, revision, cancellationToken: cancellationToken);

    /// <summary>Revokes every previously shared URL for this channel. The returned key creates a replacement URL.</summary>
    public Task<MlcgOutputChannel> RotateOutputChannelKeyAsync(string channelId, long revision, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgOutputChannel>("POST", "output-channels/" + Segment(channelId) + "/rotate-key", new { }, revision, cancellationToken: cancellationToken);

    public Task<JsonElement> DeleteOutputChannelAsync(string channelId, long revision, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<JsonElement>("DELETE", "output-channels/" + Segment(channelId), revision: revision, cancellationToken: cancellationToken);

    /// <summary>Creates a new Run and assigns the channel atomically. A stale channel revision creates no Run.</summary>
    public Task<MlcgRun> StartShowOnChannelAsync(string showId, MlcgOutputChannel channel, string idempotencyKey, string? outputId = null, bool startPaused = false, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgRun>("POST", "runs", new { showId, startPaused, outputChannelId = channel.Id, outputChannelRevision = channel.Revision, outputId = outputId ?? channel.OutputId }, requestKey: idempotencyKey, cancellationToken: cancellationToken);

    /// <summary>The scheduled start intentionally takes this channel when it fires; later manual routing is not a cancellation.</summary>
    public Task<MlcgSchedule> ScheduleShowOnChannelAsync(string showId, DateTimeOffset startAt, MlcgOutputChannel channel, string? outputId = null, CancellationToken cancellationToken = default) =>
        PlayoutRequestAsync<MlcgSchedule>("POST", "schedules", new { showId, startAt = startAt.ToUniversalTime().ToString("O"), outputChannelId = channel.Id, outputChannelRevision = channel.Revision, outputId = outputId ?? channel.OutputId }, cancellationToken: cancellationToken);

    public string BuildOutputChannelUrl(string outputBaseUrl, MlcgOutputChannel channel)
    {
        if (!Uri.TryCreate(outputBaseUrl, UriKind.Absolute, out var frontend) ||
            (frontend.Scheme != "http" && frontend.Scheme != "https") ||
            frontend.UserInfo.Length != 0 || frontend.Query.Length != 0 || frontend.Fragment.Length != 0)
            throw new ArgumentException("Use an absolute HTTP(S) frontend base without credentials, query or fragment.", nameof(outputBaseUrl));
        ValidateChannelCapability(channel);
        ValidateOutputBroker();
        return new UriBuilder(frontend) { Fragment = "/output/channel/" + Segment(channel.Id) + "?key=" + Segment(channel.OutputKey!) + "&broker=" + Segment(BaseUrl) }.Uri.AbsoluteUri;
    }

    /// <summary>Reads this single output using its capability only. ApiToken and editor login are not used.</summary>
    public async Task<MlcgOutputChannelSnapshot> GetOutputChannelSnapshotAsync(MlcgOutputChannel channel, CancellationToken cancellationToken = default)
    {
        ValidateChannelCapability(channel);
        ValidateOutputBroker();
        // A separate client deliberately cannot inherit Authorization or cookies
        // from an injected/shared control HttpClient's DefaultRequestHeaders.
        using var handler = new HttpClientHandler { UseCookies = false, UseDefaultCredentials = false, AllowAutoRedirect = false };
        using var outputClient = new HttpClient(handler) { Timeout = _httpClient.Timeout };
        return await SendJsonAsync<MlcgOutputChannelSnapshot>(outputClient, CreateRequest(HttpMethod.Get,
            "/api/v1/output/channels/" + Segment(channel.Id) + "?key=" + Segment(channel.OutputKey!),
            token: null, body: null, PlayoutJson, suppressInheritedAuth: false), PlayoutJson, cancellationToken).ConfigureAwait(false);
    }

    private static void ValidateChannelCapability(MlcgOutputChannel channel)
    {
        if (channel == null) throw new ArgumentNullException(nameof(channel));
        if (!Regex.IsMatch(channel.Id ?? "", "\\A[a-zA-Z0-9_-]{1,100}\\z") || !Regex.IsMatch(channel.OutputKey ?? "", "\\A[a-f0-9]{64}\\z"))
            throw new ArgumentException("A valid output channel ID and read-only channel key are required.", nameof(channel));
    }

    private void ValidateOutputBroker()
    {
        if ((_baseUri.Scheme != "http" && _baseUri.Scheme != "https") || _baseUri.UserInfo.Length != 0 || _baseUri.Query.Length != 0 || _baseUri.Fragment.Length != 0)
            throw new InvalidOperationException("Output connections require an HTTP(S) broker without credentials, query or fragment.");
    }

    /// <summary>Read-only channel events using its capability, not the REST control token. Reconnect explicitly after interruption.</summary>
    public async Task SubscribeOutputChannelAsync(MlcgOutputChannel channel, Func<MlcgOutputChannelEvent, Task> onEvent, CancellationToken cancellationToken = default)
    {
        ValidateChannelCapability(channel);
        ValidateOutputBroker();
        if (onEvent == null) throw new ArgumentNullException(nameof(onEvent));
        var builder = new UriBuilder(_baseUri) { Scheme = _baseUri.Scheme == "https" ? "wss" : "ws", Path = "/ws/playout", Query = "channel=" + Segment(channel.Id) + "&key=" + Segment(channel.OutputKey!) };
        using var socket = new ClientWebSocket();
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
                if (content.Length > 20_000_000) throw new InvalidOperationException("Channel event exceeds 20 MB.");
            } while (!result.EndOfMessage);
            var message = JsonSerializer.Deserialize<MlcgOutputChannelEvent>(Encoding.UTF8.GetString(content.ToArray()), PlayoutJson);
            if (message != null) await onEvent(message).ConfigureAwait(false);
        }
    }
}
