using System.Text.Json.Serialization;

namespace MLCG.RestControl.Models;

internal sealed class SessionListEnvelope
{
    [JsonPropertyName("sessions")]
    public List<MlcgSessionSummary> Sessions { get; set; } = new();

    [JsonPropertyName("summaries")]
    public List<MlcgSessionSummary> Summaries { get; set; } = new();
}

internal sealed class BindingsEnvelope
{
    [JsonPropertyName("bindings")]
    public List<MlcgBinding> Bindings { get; set; } = new();
}

internal sealed class ElementsEnvelope
{
    [JsonPropertyName("elements")]
    public List<MlcgElement> Elements { get; set; } = new();
}
