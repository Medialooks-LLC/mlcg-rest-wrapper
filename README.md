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

The public **1.0.1** package includes typed Show/Timeline/Clip/Run/Schedule methods, live clip-data updates, atomic live-plan edits, JSON import/export, run WebSocket subscriptions and permanent Output Channel methods. See [the scheduling guide](../../docs/playout-scheduling.md) for the contract and examples. Existing session methods remain compatible. The temporary Demo Workspace methods described below are new development source and are **not included in public 1.0.1**.

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

The updated source adds `PlayAnimationAsync`, `PauseAnimationAsync`,
`RestartAnimationAsync`, `SeekAnimationAsync`, `SetAnimationLoopAsync`,
`DeleteSessionAsync` and `BuildOverlayUrl`. These additions, Demo Workspace and
animation metadata are **not in public NuGet 1.0.1**. Installing 1.0.1 does not
enable them; use an updated build until a matching package is published.

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

The updated **source** also has `GetSessionAnimationInfoAsync(sessionId)` for
the exact finite scene range and intrinsic loop region. This read-only method
is not in NuGet 1.0.1; it reads the existing session snapshot and does not alter
templates or playback. Its typed result describes timing, not the current
painted frame. The SDK sample's animation slider uses this range with the
existing `pause`/`play`/`set-loop` commands, independently of video position.

For new integrations using the updated client, prefer the named helpers:

```csharp
var outputUrl = client.BuildOverlayUrl("https://cg.medialooks.com", sessionId);
// Pass outputUrl to the Video SDK HTML5 Overlay plugin; no control token is in it.
await client.UpdateBindingAsync(sessionId, "headline", "Updated from C#");
await client.RestartAnimationAsync(sessionId, loop: false);
await client.PauseAnimationAsync(sessionId);
await client.SeekAnimationAsync(sessionId, positionMs: 1500); // Seek and stay paused.
await client.PlayAnimationAsync(sessionId);                  // Resume current position.
await client.SetAnimationLoopAsync(sessionId, enabled: true); // No restart or seek.
```

`BuildOverlayUrl` validates both HTTP(S) bases and defaults to a transparent
background, autoplay on and whole-scene Loop off. Its URL is a read-only output
capability: share it only with intended viewers. `SeekAnimationAsync` sends one
atomic pause-at-position command and does not change looping. Omitting position
or loop in the other helpers leaves that setting unchanged. Intrinsic per-layer
loops and animation definitions are not rewritten. These methods control all
subscribers of the selected session, not the application's source video.

`DeleteSessionAsync(sessionId)` removes that live session subject to server
permissions. In Demo it frees a private-instance slot; it does not delete the
stock design. Its output URLs stop working. The library never evicts sessions
or retries mutating commands automatically.

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

See the [complete channel REST contract](../../docs/playout-scheduling.md#permanent-output-channels) and [C# integration examples](../../docs/csharp-rest-wrapper.md#permanent-output-channels). These channel methods are available in public NuGet 1.0.1.

## Temporary Demo Workspace (updated client required)

This opt-in server feature gives each application launch its own temporary, in-memory graphics instances. It does not create an account, save edits to the template library, or expose other launches. The catalog consists of real server templates, read-only; each selected template is instantiated independently. A single instance's output URL can be used by multiple viewers, all receiving the same updates.

**Hosted at `https://cg.medialooks.com`; not shipped in NuGet 1.0.1.** Use a compatible updated package containing these methods (1.0.2 or later), or a project reference to the matching source. A source reference is not required when the updated NuGet package is used. Until a new version is published, use the supplied local preview feed rather than assuming it is available on nuget.org. Self-hosted servers must explicitly enable demo workspaces. Keep an ordinary account connection in a different client instance. No shared account password or permanent demo token is embedded in the application.

```csharp
using MLCG.RestControl;

using var demo = new MlcgRestClient("https://cg.medialooks.com");
var workspace = await demo.StartDemoWorkspaceAsync(
    Guid.NewGuid().ToString("D"), "mplatform-rest-overlay", "1.0.0");
// Creation is unauthenticated and uses a separate transport: it sends neither
// demo.ApiToken nor credentials/cookies on an injected control HttpClient.
// Show workspace.UsageNotice to the user before continuing.
demo.ApiToken = workspace.ApiToken!; // Explicitly scope this separate client.

var templates = await demo.GetDemoTemplatesAsync();
var instance = await demo.CreateDemoSessionAsync(
    workspace.Id, templates.First().Id, Guid.NewGuid().ToString("D"));
var bindings = await demo.GetBindingsAsync(instance.Session.Id);
// Choose a text key actually exposed by the selected design, not a color/source key.
if (bindings.Any(binding => binding.Key == "headline"))
    await demo.UpdateBindingAsync(instance.Session.Id, "headline", "Hello from the sample");
var outputUrl = demo.BuildOutputUrl("https://cg.medialooks.com",
    instance.Session.Id, background: "transparent");
// Give outputUrl to the SDK HTML5 overlay. Never put ApiToken in the URL.
await demo.SendTimelineCommandAsync(instance.Session.Id, "restart");

// Renew periodically while this controller is running (normally 30 seconds).
await demo.HeartbeatDemoWorkspaceAsync(workspace.Id);
// On close, release control; existing read-only viewers can finish normally.
await demo.ReleaseDemoWorkspaceAsync(workspace.Id);
// EndDemoWorkspaceAsync instead explicitly revokes the entire scope and outputs.
```

Reuse a `launchId` only when retrying creation after a transport failure in the same launch. Likewise, reuse an `instanceKey` only when retrying the same template instantiation. A new launch/intentional instance needs a new UUID. Creation does not automatically change `ApiToken`; subsequent methods require the returned workspace token. Heartbeat responses update lease information and do not return the control token again.

| Method | REST call | Purpose |
| --- | --- | --- |
| `StartDemoWorkspaceAsync` | `POST /api/demo/workspaces` | Creates an isolated scope without account authentication. |
| `GetDemoTemplatesAsync` | `GET /api/demo/templates` | Lists the read-only demo catalog. |
| `CreateDemoSessionAsync` | `POST /api/demo/workspaces/{id}/sessions` | Creates an independent instance and returns its bindings. |
| `HeartbeatDemoWorkspaceAsync` | `POST .../{id}/heartbeat` | Renews the controller lease within the maximum lifetime. |
| `ReleaseDemoWorkspaceAsync` | `POST .../{id}/release` | Releases control without immediately ending connected viewers. |
| `EndDemoWorkspaceAsync` | `DELETE /api/demo/workspaces/{id}` | Explicitly ends the scope and its instances. |

Ordinary `GetSessionsAsync`, binding updates and timeline commands work within the returned scope; account/session enumeration outside it is not allowed. Demo state expires after inactivity or the maximum lifetime and is lost when the service restarts. Do not rely on it for production playout. Server limits and expiry timestamps are authoritative. Availability is checked by creation itself, not by the generic health endpoint. A disabled demo feature returns an error; do not silently fall back to a shared privileged token.

The server may retain aggregate demo-usage counters, separately from temporary scene data. Display the returned usage notice. Only an administrator can read these statistics; the demo token cannot access them. Never log control tokens, binding content, video filenames or complete output URLs as telemetry.

## API Token

When using a hosted/account REST service, generate a REST token in the web editor and assign it to `ApiToken`.

The client sends the token as:

* `Authorization: Bearer <token>`
* `x-mlcg-token: <token>`

## HTTP Ownership and Error Handling

In the updated source, every request captures its own authentication headers.
The library does not modify a supplied `HttpClient.DefaultRequestHeaders` and
does not dispose a caller-owned `HttpClient`; it disposes its requests and
responses. A blank `ApiToken` suppresses inherited control-auth headers for that
request. Use separate `MlcgRestClient` instances for different services/accounts
rather than reconfiguring one client while operations are active.

The default client does not follow redirects. When injecting an `HttpClient`,
configure its handler with `AllowAutoRedirect = false`: the wrapper cannot
change an existing handler's redirect or cookie policy. In particular, do not
allow custom control-token headers to be forwarded to another host.

HTTP failures in the updated client throw `MlcgRestException`, which still
derives from `HttpRequestException`. `StatusCode` is nullable when no response
was received; `RetryAfter` is a nullable delay, clamped to zero through 24 hours.
No retry is automatic. Messages do not include server bodies, request URLs,
capability keys or credentials. Caller cancellation remains cancellation.

```csharp
try
{
    await client.SetAnimationLoopAsync(sessionId, enabled: true);
}
catch (MlcgRestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
{
    // Ask the user to reconnect; never log or replace their token implicitly.
}
catch (MlcgRestException ex) when ((int?)ex.StatusCode == 429)
{
    // Offer a retry after ex.RetryAfter. Do not silently replay on-air commands.
}
```

The public 1.0.1 API exposes `HttpRequestException`; existing catch blocks remain
compatible with the updated source.

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
