using System.Net.Http;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    /// <summary>
    /// Starts or resumes a session's animation. Omit positionMs to resume its
    /// current position, and omit loop to leave its loop setting unchanged.
    /// This does not start or seek the application's source video.
    /// </summary>
    public Task PlayAnimationAsync(
        string sessionId,
        int? positionMs = null,
        bool? loop = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePlaybackSessionId(sessionId);
        ValidateAnimationPosition(positionMs);
        cancellationToken.ThrowIfCancellationRequested();
        return SendTimelineCommandAsync(sessionId, "play", positionMs, loop,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Pauses a session's animation, optionally at an exact position. Does not
    /// change its loop setting or pause the application's source video.
    /// </summary>
    public Task PauseAnimationAsync(
        string sessionId,
        int? positionMs = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePlaybackSessionId(sessionId);
        ValidateAnimationPosition(positionMs);
        cancellationToken.ThrowIfCancellationRequested();
        return SendTimelineCommandAsync(sessionId, "pause", positionMs,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Restarts a session's animation from its beginning. Omit loop to leave
    /// its loop setting unchanged. Source-video playback is unaffected.
    /// </summary>
    public Task RestartAnimationAsync(
        string sessionId,
        bool? loop = null,
        CancellationToken cancellationToken = default)
    {
        ValidatePlaybackSessionId(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        return SendTimelineCommandAsync(sessionId, "restart", loop: loop,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Seeks and holds the animation with one atomic pause-at-position command.
    /// It does not resume playback, change looping, or seek source video.
    /// Positions beyond the scene are clamped by the output's existing rules.
    /// </summary>
    public Task SeekAnimationAsync(
        string sessionId,
        int positionMs,
        CancellationToken cancellationToken = default)
    {
        return PauseAnimationAsync(sessionId, positionMs, cancellationToken);
    }

    /// <summary>
    /// Changes animation looping without restarting or seeking. Intrinsic
    /// per-layer repeat/loop behavior is not rewritten by this setting.
    /// </summary>
    public Task SetAnimationLoopAsync(
        string sessionId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        ValidatePlaybackSessionId(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        return SendTimelineCommandAsync(sessionId, "set-loop", loop: enabled,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Removes a live session, subject to the server's ownership permissions.
    /// For a demo workspace this frees an instance slot; it does not delete the
    /// catalog template. Connected outputs of that instance stop receiving it.
    /// </summary>
    public Task DeleteSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        ValidatePlaybackSessionId(sessionId);
        cancellationToken.ThrowIfCancellationRequested();
        return SendSessionRequestAsync(HttpMethod.Delete,
            "/api/sessions/" + Uri.EscapeDataString(sessionId), cancellationToken);
    }

    /// <summary>
    /// Builds a read-only transparent HTML overlay URL. Control credentials are
    /// never included. The session identifier is still an output capability:
    /// share this URL only with intended viewers. Both service bases must be
    /// HTTP(S) URLs without credentials, query strings or fragments.
    /// </summary>
    public string BuildOverlayUrl(
        string outputBaseUrl,
        string sessionId,
        bool autoplay = true,
        bool loop = false)
    {
        ValidatePlaybackSessionId(sessionId);
        var output = ValidateOverlayBaseUrl(outputBaseUrl, nameof(outputBaseUrl));
        var rest = ValidateOverlayBaseUrl(BaseUrl, nameof(BaseUrl));
        var id = Uri.EscapeDataString(sessionId);
        var service = Uri.EscapeDataString(rest.AbsoluteUri.TrimEnd('/'));
        return output.AbsoluteUri.TrimEnd('/') + "/#/output/" + id
            + "?autoplay=" + (autoplay ? "1" : "0")
            + "&loop=" + (loop ? "1" : "0")
            + "&bg=transparent&broker=" + service + "&rest=" + service + "&session=" + id;
    }

    private static void ValidatePlaybackSessionId(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 256
            || sessionId == "." || sessionId == ".." || sessionId.Any(char.IsControl))
            throw new ArgumentException("A non-empty session identifier of at most 256 characters without control characters is required.", nameof(sessionId));
    }

    private static void ValidateAnimationPosition(int? positionMs)
    {
        if (positionMs < 0)
            throw new ArgumentOutOfRangeException(nameof(positionMs), "Animation position must be non-negative milliseconds.");
    }

    private static Uri ValidateOverlayBaseUrl(string baseUrl, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Any(char.IsControl)
            || baseUrl.IndexOf('\\') >= 0
            || !Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
            throw new ArgumentException("Use an HTTP(S) base URL without credentials, query or fragment.", parameterName);
        return uri;
    }
}
