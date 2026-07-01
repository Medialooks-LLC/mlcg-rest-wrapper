using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgSize
{
    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }
}
