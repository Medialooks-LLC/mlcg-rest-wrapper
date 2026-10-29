namespace MLCG.RestControl.Models;

/// <summary>Finite preview range and intrinsic loop region of a published session.</summary>
public sealed class MlcgAnimationInfo
{
    public int DurationMs { get; set; }
    public int LoopStartMs { get; set; }
    public int LoopEndMs { get; set; }
    public bool HasIntrinsicLoop { get; set; }
    public double Fps { get; set; } = 50;
}
