using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgElement
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("parentId")]
    public string? ParentId { get; set; }

    [JsonPropertyName("depth")]
    public int Depth { get; set; }

    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("visible")]
    public bool Visible { get; set; }

    [JsonPropertyName("animation")]
    public string Animation { get; set; } = string.Empty;

    [JsonPropertyName("delay")]
    public int Delay { get; set; }

    [JsonPropertyName("duration")]
    public int Duration { get; set; }

    [JsonPropertyName("position")]
    public MlcgPoint Position { get; set; } = new();

    [JsonPropertyName("size")]
    public MlcgSize Size { get; set; } = new();

    public string DisplayName => $"{new string(' ', Depth * 2)}{Name}";
}
