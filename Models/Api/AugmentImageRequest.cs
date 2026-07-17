using System.Text.Json.Serialization;

namespace ImageGen.Models.Api;

public class AugmentImageRequest
{
    public string req_type { get; set; } = string.Empty;
    public int width { get; set; }
    public int height { get; set; }
    public string image { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? defry { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? prompt { get; set; }
}
