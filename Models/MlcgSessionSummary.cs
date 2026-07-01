using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

public sealed class MlcgSessionSummary
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("templateName")]
    public string TemplateName { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("system")]
    public string System { get; set; } = string.Empty;

    [JsonPropertyName("width")]
    public int Width { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("fps")]
    public double Fps { get; set; }

    [JsonPropertyName("bindingCount")]
    public int BindingCount { get; set; }

    [JsonPropertyName("elementCount")]
    public int ElementCount { get; set; }

    public string Summary => $"{Width} x {Height} / {Fps:0.##} fps";

    public string DisplayName
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Name) ? Id : Name;
            if (!string.IsNullOrWhiteSpace(TemplateName) &&
                !string.Equals(TemplateName, title, StringComparison.OrdinalIgnoreCase))
            {
                return title + " / " + TemplateName;
            }

            return title;
        }
    }
}
