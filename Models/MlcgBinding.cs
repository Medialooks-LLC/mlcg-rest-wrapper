using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgBinding
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;

    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? Key : Label;
}
