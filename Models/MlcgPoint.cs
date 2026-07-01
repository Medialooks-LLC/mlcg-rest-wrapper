using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgPoint
{
    [JsonPropertyName("x")]
    public int X { get; set; }

    [JsonPropertyName("y")]
    public int Y { get; set; }
}
