# MLCG.RestControl

C# wrapper for controlling MLCG live sessions through the REST API.

MLCG is the MediaLooks HTML5 CG editor and live graphics service. Operators use it to create, publish, and play browser-rendered broadcast graphics such as lower thirds, scorebugs, tickers, fullscreen boards, and other live templates.

- Hosted service: https://cg.medialooks.com/
- Documentation: https://docs.medialooks.com/mlcg

Use this library from a desktop controller, automation tool, newsroom system, scoreboard integration, or any C# application that needs to read sessions, update live binding values, build Show playout and deliver it through a permanent HTML output link.

## Install

```powershell
dotnet add package MLCG.RestControl
```

## Client Setup

```csharp
using MLCG.RestControl;

using var client = new MlcgRestClient("https://cg.medialooks.com");
client.ApiToken = "mlcg_your_rest_token"; // Required for hosted session-control endpoints.
```

For a local broker, use:

```csharp
using var client = new MlcgRestClient("http://127.0.0.1:3101");
```

## Commands

The `codex/show-timeline-playout` development branch adds typed Show/Timeline/Clip/Run/Schedule methods, live clip-data updates, atomic live-plan edits, JSON import/export and run WebSocket subscriptions. The updated source also includes permanent Output Channel methods. Build a wrapper containing this source to use them; the public 1.0.0 package does not contain them yet. See [the scheduling guide](../../docs/playout-scheduling.md) for the contract and examples. Existing session methods remain compatible.

| Method | REST call | Purpose |
| --- | --- | --- |
| `GetHealthAsync()` | `GET /api/health` | Checks server health and session count. |
| `GetSessionsAsync()` | `GET /api/sessions` | Lists live sessions published by the editor. |
| `GetBindingsAsync(sessionId)` | `GET /api/sessions/:id/bindings` | Gets editable binding values for one session. |
| `GetElementsAsync(sessionId)` | `GET /api/sessions/:id/elements` | Gets the flattened element list for one session. |
| `UpdateBindingAsync(sessionId, key, value)` | `PATCH /api/sessions/:id/bindings` | Updates one binding value. |
| `UpdateBindingsAsync(sessionId, values)` | `PATCH /api/sessions/:id/bindings` | Updates multiple binding values. |
| `ReplaceBindingsAsync(sessionId, bindings)` | `PUT /api/sessions/:id/bindings` | Replaces the full binding list. |
| `SendTimelineCommandAsync(sessionId, command, ...)` | `POST /api/sessions/:id/timeline` | Controls output play, pause, stop, restart, seek, or loop mode. |
| `BuildOutputUrl(outputBaseUrl, sessionId)` | n/a | Builds a live output URL for a session. |

## Read Sessions

```csharp
var health = await client.GetHealthAsync();
Console.WriteLine($"REST online: {health.Ok}, sessions: {health.SessionCount}");

var sessions = await client.GetSessionsAsync();

foreach (var session in sessions)
{
    Console.WriteLine($"{session.Id}: {session.Name} ({session.BindingCount} bindings)");
}
```

## Read Bindings and Elements

```csharp
var session = (await client.GetSessionsAsync()).First();

var bindings = await client.GetBindingsAsync(session.Id);
var elements = await client.GetElementsAsync(session.Id);

foreach (var binding in bindings)
{
    Console.WriteLine($"{binding.Key} = {binding.Value}");
}

foreach (var element in elements)
{
    Console.WriteLine($"{element.DisplayName} / {element.Type} / {element.Animation}");
}
```

## Update One Binding

```csharp
await client.UpdateBindingAsync(
    sessionId: "live-show-main",
    key: "player_name",
    value: "MARTA KOVACS");
```

## Update Multiple Bindings

```csharp
var updatedBindings = await client.UpdateBindingsAsync(
    "live-show-main",
    new Dictionary<string, string>
    {
        ["player_name"] = "MARTA KOVACS",
        ["team_name"] = "RIVER CITY FC",
        ["score"] = "2 - 1",
    });
```

The REST server broadcasts binding updates to subscribed live output pages.

## Control Timeline Playback

```csharp
await client.SendTimelineCommandAsync("live-show-main", "restart");
await client.SendTimelineCommandAsync("live-show-main", "set-loop", loop: false);
```

Supported commands are `play`, `pause`, `stop`, `restart`, `seek`, and `set-loop`. `seek` requires `positionMs`; `set-loop` requires `loop`.

## Replace All Bindings

Use `ReplaceBindingsAsync` only when your application owns the complete binding list.

```csharp
var currentBindings = await client.GetBindingsAsync("live-show-main");

var replacement = currentBindings.Select(binding => new MlcgBinding
{
    Id = binding.Id,
    Key = binding.Key,
    Label = binding.Label,
    Value = binding.Key == "player_name" ? "MARTA KOVACS" : binding.Value,
});

await client.ReplaceBindingsAsync("live-show-main", replacement);
```

For normal operator control, prefer `UpdateBindingAsync` or `UpdateBindingsAsync`.

## Build a Live Output URL

```csharp
var outputUrl = client.BuildOutputUrl(
    outputBaseUrl: "https://cg.medialooks.com",
    sessionId: "live-show-main",
    background: "transparent",
    autoplay: true,
    loop: true);

Console.WriteLine(outputUrl);
```

`BuildOutputUrl` includes both `broker` and legacy `rest` query parameters so output pages can subscribe to live binding updates.

## Permanent Output Channels

Give the final user a permanent channel URL for their browser/HTML overlay. Unlike `BuildShowOutputUrl`, which belongs to one Run, this link follows whichever Run and output the operator explicitly assigns to the channel.

```csharp
using MLCG.RestControl;
using MLCG.RestControl.Models;

using var client = new MlcgRestClient("https://cg.medialooks.com");
client.ApiToken = tokenFromYourApplication;

var channel = await client.EnsureDefaultOutputChannelAsync();
var url = client.BuildOutputChannelUrl("https://cg.medialooks.com", channel);
// Share url privately with the final user, never ApiToken.

var run = await client.StartShowOnChannelAsync(
    showId, channel, Guid.NewGuid().ToString("N"), outputId: "program");

// Reload before the next take: the previous assignment changed Revision.
channel = await client.GetOutputChannelAsync(channel.Id);
var nextRun = await client.StartShowOnChannelAsync(
    nextShowId, channel, Guid.NewGuid().ToString("N"), outputId: "program");
// The original url is unchanged.
```

`EnsureDefaultOutputChannelAsync` idempotently provisions the current account's Program destination without starting or routing a Run. `StartShowOnChannelAsync` atomically starts and assigns; a stale channel revision returns `412` and creates no Run. Reuse the same idempotency key for a transport retry with identical Show/start-mode/routing intent. Retrying returns that original Run without rerouting it if another Run has since taken the channel. A new intentional execution needs a new key; different intent with the old key returns `409`.

| Wrapper method | REST contract |
| --- | --- |
| `GetOutputChannelsAsync`, `GetOutputChannelAsync` | `GET /api/v1/output-channels[/{id}]` (authenticated). Lists contain only the current account's channels. |
| `EnsureDefaultOutputChannelAsync` | `POST /api/v1/output-channels` with `{ "name": "Program", "default": true }`. |
| `CreateOutputChannelAsync` | `POST /api/v1/output-channels` with a name and optional output. Creates an independent destination. |
| `RenameOutputChannelAsync` | `PATCH /api/v1/output-channels/{id}` with the channel's current revision. |
| `AssignOutputChannelAsync`, `ClearOutputChannelAsync` | `PUT /api/v1/output-channels/{id}/assignment`, current revision. `runId: null` clears the channel without stopping its Run. |
| `RotateOutputChannelKeyAsync`, `DeleteOutputChannelAsync` | Revoke old links via `POST .../{id}/rotate-key` or `DELETE .../{id}`, current revision. |
| `StartShowOnChannelAsync` | `POST /api/v1/runs`, with routing intent, `outputChannelRevision` and `Idempotency-Key`. |
| `ScheduleShowOnChannelAsync` | `POST /api/v1/schedules`, storing the destination/output with the pinned Show snapshot. |
| `BuildOutputChannelUrl` | Builds `#/output/channel/{id}?key={outputKey}&broker={BaseUrl}`; no control token or editable output selector. |
| `GetOutputChannelSnapshotAsync` | Read-only `GET /api/v1/output/channels/{id}?key=...`; uses only the capability, not control credentials/cookies. |
| `SubscribeOutputChannelAsync` | Read-only `WS /ws/playout?channel={id}&key=...`. Reconnect explicitly after interruption. |

The read-only snapshot endpoint is `GET /api/v1/output/channels/{id}?key=...`. Channel snapshots and `channel.clock` events are server-scoped to the assigned output; other outputs and the Run's broader output key are excluded. `channel.snapshot` can contain `Run = null` for an idle, transparent channel. Replace the retained Run/plan when assignment changes. A channel capability cannot control the Run or list normal account objects, but it does reveal its graphics/data, so keep the URL private. Renaming, clearing and reassignment keep it unchanged; key rotation or channel deletion revokes it, including open subscriptions.

```csharp
// These read-only calls use channel.OutputKey, not client.ApiToken.
var snapshot = await client.GetOutputChannelSnapshotAsync(channel);
Console.WriteLine(snapshot.Run?.Name ?? "Channel idle (transparent)");
await client.SubscribeOutputChannelAsync(channel, message =>
{
    Console.WriteLine(message.Type); // channel.snapshot or channel.clock
    return Task.CompletedTask;
}, cancellationToken);
```

`ScheduleShowOnChannelAsync` checks the supplied revision at creation, then intentionally takes that channel when the scheduled Run starts. A later manual reassignment does not cancel the job. Cancel it explicitly with `CancelScheduleAsync`; a deleted/unavailable channel fails the scheduled start. Ordinary `StartShowAsync`/`ScheduleShowAsync` do not route channels. For remote sharing, neither frontend nor broker may point at `localhost` on the recipient's machine.

See the [complete channel REST contract](../../docs/playout-scheduling.md#permanent-output-channels) and [C# integration examples](../../docs/csharp-rest-wrapper.md#permanent-output-channels). These source methods are not a claim about availability in an already-installed public NuGet release.

## API Token

When using a hosted/account REST service, generate a REST token in the web editor and assign it to `ApiToken`.

The client sends the token as:

* `Authorization: Bearer <token>`
* `x-mlcg-token: <token>`

## Error Handling

The wrapper uses `HttpResponseMessage.EnsureSuccessStatusCode()`. Failed REST calls throw `HttpRequestException`.

```csharp
try
{
    await client.UpdateBindingAsync("live-show-main", "player_name", "MARTA KOVACS");
}
catch (HttpRequestException ex)
{
    Console.WriteLine($"REST update failed: {ex.Message}");
}
```

For the full integration guide, see https://docs.medialooks.com/mlcg.
