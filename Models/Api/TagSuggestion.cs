using System.Text.Json.Serialization;

namespace ImageGen.Models.Api;

public class TagSuggestionResponse
{
    [JsonPropertyName("tags")]
    public List<TagSuggestion> Tags { get; set; } = new List<TagSuggestion>();
}

public class TagSuggestion
{
    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("count")]
    public int Count { get; set; }
    
    [JsonPropertyName("confidence")]
    public double Confidence { get; set; }
}
