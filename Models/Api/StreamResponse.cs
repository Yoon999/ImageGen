using System.Text.Json.Serialization;

namespace ImageGen.Models.Api;

public class StreamResponse
{
    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string? Image { get; set; }

    [JsonPropertyName("step_ix")]
    public int StepIndex { get; set; }

    [JsonPropertyName("gen_id")]
    public long GenId { get; set; }
}
