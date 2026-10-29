using System.Text.Json;
using MLCG.RestControl.Models;

namespace MLCG.RestControl;

public sealed partial class MlcgRestClient
{
    private sealed class AnimationSessionEnvelope
    {
        public JsonElement Session { get; set; }
    }

    /// <summary>
    /// Gets the scene duration and loop region using the web editor's stage,
    /// legacy-animation and repeat rules. This reads the existing session route;
    /// it neither seeks nor changes playback. Use a client package containing
    /// this method or the matching source; public version 1.0.1 does not include it.
    /// </summary>
    public async Task<MlcgAnimationInfo> GetSessionAnimationInfoAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId) || sessionId.Length > 256)
            throw new ArgumentException("A session identifier of at most 256 characters is required.", nameof(sessionId));
        cancellationToken.ThrowIfCancellationRequested();
        var payload = await GetAsync<AnimationSessionEnvelope>(
            $"/api/sessions/{Uri.EscapeDataString(sessionId)}", cancellationToken).ConfigureAwait(false);
        var template = AnimationProperty(payload.Session, "template");
        var layers = AnimationProperty(template, "layers");
        if (template.ValueKind != JsonValueKind.Object || layers.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("The published session has no valid template layers.");

        // Mirror App.tsx getLoopTimelineInfo/getSceneDuration. Stage durations
        // use JS Math.round semantics (ties go toward +infinity), not banker's
        // rounding. Visibility, pinning and keyframes do not change this range.
        double loopStart = 2200;
        double loopDuration = 0;
        bool hasLoopLayer = false;
        var pending = new Stack<(JsonElement Layers, int Depth)>();
        pending.Push((layers, 0));
        var visited = 0;
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = pending.Pop();
            if (current.Depth > 32) throw new InvalidOperationException("The published session contains too many nested layers.");
            foreach (var layer in current.Layers.EnumerateArray())
            {
                if (++visited > 10000) throw new InvalidOperationException("The published session contains too many layers.");
                if (layer.ValueKind != JsonValueKind.Object) continue;
                var children = AnimationProperty(layer, "children");
                if (children.ValueKind == JsonValueKind.Array && children.GetArrayLength() > 0)
                    pending.Push((children, current.Depth + 1));

                var legacyDuration = AnimationNumber(AnimationProperty(layer, "duration"), 420);
                if (legacyDuration == 0) legacyDuration = 420; // layer.duration || 420
                var inFallback = Math.Max(120, legacyDuration);
                var legacyAnimation = AnimationString(AnimationProperty(layer, "animation")) ?? "Fade In";
                if (legacyAnimation.Length == 0) legacyAnimation = "Fade In";
                var inDuration = AnimationStageDuration(layer, "in", legacyAnimation, inFallback);
                var showDuration = AnimationStageDuration(layer, "show", "Hold", 1400);
                var outDuration = AnimationStageDuration(layer, "out", "None", 0);
                var cycle = inDuration + showDuration + outDuration;
                var mode = AnimationString(AnimationProperty(layer, "repeatMode")) ?? "once";
                var repeated = mode == "count"
                    ? cycle * Math.Max(1, Math.Min(99, Math.Floor(AnimationNumber(AnimationProperty(layer, "repeatCount"), 1) + .5)))
                    : cycle;
                var timelineDuration = repeated + (showDuration <= 0 && outDuration <= 0 ? 1400 : 0);
                var delay = Math.Max(0, AnimationNumber(AnimationProperty(layer, "delay"), 0));
                loopStart = Math.Max(loopStart, delay + (mode == "loop" ? cycle : timelineDuration));
                if (mode == "loop") { hasLoopLayer = true; loopDuration = Math.Max(loopDuration, cycle); }
            }
        }
        var hasLoop = hasLoopLayer && loopDuration > 0;
        var loopEnd = loopStart + Math.Max(loopDuration, 1000);
        var fps = AnimationNumber(AnimationProperty(template, "fps"), 50);
        if (fps < 1 || fps > 240) fps = 50;
        return new MlcgAnimationInfo
        {
            DurationMs = AnimationMilliseconds(hasLoop ? loopEnd : loopStart),
            LoopStartMs = AnimationMilliseconds(loopStart),
            LoopEndMs = AnimationMilliseconds(loopEnd),
            HasIntrinsicLoop = hasLoop,
            Fps = fps,
        };
    }

    private static JsonElement AnimationProperty(JsonElement item, string name) =>
        item.ValueKind == JsonValueKind.Object && item.TryGetProperty(name, out var value) ? value : default;

    private static string? AnimationString(JsonElement item) =>
        item.ValueKind == JsonValueKind.String ? item.GetString() : null;

    private static double AnimationNumber(JsonElement item, double fallback)
    {
        if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out var value) || double.IsNaN(value) || double.IsInfinity(value)) return fallback;
        return Math.Max(-int.MaxValue, Math.Min(int.MaxValue, value));
    }

    private static double AnimationStageDuration(JsonElement layer, string stageName, string fallbackAnimation, double fallbackDuration)
    {
        var stage = AnimationProperty(AnimationProperty(layer, "animationStages"), stageName);
        var animation = AnimationString(AnimationProperty(stage, "animation")) ?? fallbackAnimation;
        if (animation == "None") return 0;
        return Math.Max(0, Math.Floor(AnimationNumber(AnimationProperty(stage, "duration"), fallbackDuration) + .5));
    }

    private static int AnimationMilliseconds(double value) =>
        (int)Math.Max(0, Math.Min(int.MaxValue, Math.Floor(value + .5)));
}
