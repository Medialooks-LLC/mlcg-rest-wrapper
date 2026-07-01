# MLCG.RestControl

C# wrapper for controlling MLCG live sessions through the REST API.

MLCG is the MediaLooks HTML5 CG editor and live graphics service. Operators use it to create, publish, and play browser-rendered broadcast graphics such as lower thirds, scorebugs, tickers, fullscreen boards, and other live templates.

- Hosted service: https://cg.medialooks.com/
- Documentation: https://docs.medialooks.com/mlcg

Use this library from a desktop controller, automation tool, newsroom system, scoreboard integration, or any C# application that needs to read sessions and update live binding values.

## Install

```powershell
dotnet add package MLCG.RestControl
```

## Client Setup

```csharp
using MLCG.RestControl;

using var client = new MlcgRestClient("https://cg.medialooks.com");
client.ApiToken = "mlcg_your_rest_token"; // Optional when the hosted service requires account access.
```

For a local broker, use:

```csharp
using var client = new MlcgRestClient("http://127.0.0.1:3101");
```

## Commands

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
