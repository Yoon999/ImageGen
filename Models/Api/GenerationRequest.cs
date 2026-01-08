using System.Text.Json.Serialization;

namespace ImageGen.Models.Api;

public class GenerationRequest
{
    public string input { get; set; } = string.Empty;
    public string model { get; set; } = "nai-diffusion-4-5-full";
    public string action { get; set; } = "generate";
    public RequestParameters parameters { get; set; } = new RequestParameters();
}

public class RequestParameters
{
    public int width { get; set; } = 832;
    public int height { get; set; } = 1216;
    public double scale { get; set; } = 5.0;
    public string sampler { get; set; } = "k_euler_ancestral";
    public int steps { get; set; } = 28;
    public long seed { get; set; }
    public int n_samples { get; set; } = 1;
    
    public bool sm { get; set; } = false;
    public bool sm_dyn { get; set; } = false;
    
    public double cfg_rescale { get; set; } = 0.0;
    public string noise_schedule { get; set; } = "karras";
    public bool qualityToggle { get; set; } = true;
    public string uc { get; set; } = string.Empty; // Negative Prompt (Legacy or V3)
    public bool dynamic_thresholding { get; set; } = false;
    public bool deliberate_euler_ancestral_bug { get; set; } = false;
    public bool prefer_brownian { get; set; } = true;

    // V4 관련 파라미터
    [JsonPropertyName("v4_prompt")]
    public V4ConditionInput? V4Prompt { get; set; }

    [JsonPropertyName("v4_negative_prompt")]
    public V4ConditionInput? V4NegativePrompt { get; set; }
}

public class V4ConditionInput
{
    [JsonPropertyName("caption")]
    public V4ExternalCaption Caption { get; set; } = new V4ExternalCaption();

    [JsonPropertyName("legacy_uc")]
    public bool LegacyUc { get; set; } = false;

    [JsonPropertyName("use_coords")]
    public bool UseCoords { get; set; } = false;

    [JsonPropertyName("use_order")]
    public bool UseOrder { get; set; } = false;
}

public class V4ExternalCaption
{
    [JsonPropertyName("base_caption")]
    public string BaseCaption { get; set; } = string.Empty;

    [JsonPropertyName("char_captions")]
    public List<V4ExternalCharacterCaption> CharCaptions { get; set; } = new List<V4ExternalCharacterCaption>();
}

public class V4ExternalCharacterCaption
{
    [JsonPropertyName("char_caption")]
    public string CharCaption { get; set; } = string.Empty;

    [JsonPropertyName("centers")]
    public List<Coordinates> Centers { get; set; } = new List<Coordinates>();
}

public class Coordinates
{
    public double x { get; set; }
    public double y { get; set; }
}
