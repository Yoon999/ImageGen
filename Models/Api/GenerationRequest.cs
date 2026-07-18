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
    
    [JsonPropertyName("controlnet_strength")]
    public double controlnet_strength { get; set; } = 1.0;
    public int steps { get; set; } = 28;
    public long seed { get; set; }
    public int n_samples { get; set; } = 1;
    
    public bool sm { get; set; } = false;
    public bool sm_dyn { get; set; } = false;
    
    public double cfg_rescale { get; set; } = 0.0;
    public string noise_schedule { get; set; } = "karras";
    public bool qualityToggle { get; set; } = true;
    public bool legacy { get; set; } = false;
    public bool legacy_v3_extend { get; set; } = false;
    public double uncond_scale { get; set; } = 1.0;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? skip_cfg_above_sigma { get; set; }
    
    [JsonPropertyName("uc")]
    public string uc { get; set; } = string.Empty; // Negative Prompt
    public string negative_prompt { get; set; } = string.Empty;
    public string prompt { get; set; } = string.Empty;
    public bool dynamic_thresholding { get; set; } = false;
    public bool deliberate_euler_ancestral_bug { get; set; } = false;
    public bool prefer_brownian { get; set; } = true;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? image { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? mask { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Img2ImgParameters? img2img { get; set; }

    public bool add_original_image { get; set; } = false;

    public List<string> reference_image_multiple { get; set; } = new();
    public List<double> reference_information_extracted_multiple { get; set; } = new();
    public List<double> reference_strength_multiple { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? director_reference_images { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<DirectorReferenceDescription>? director_reference_descriptions { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? director_reference_strength_values { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? director_reference_secondary_strength_values { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<double>? director_reference_information_extracted { get; set; }

    // V4 관련 파라미터
    [JsonPropertyName("v4_prompt")]
    public V4ConditionInput? V4Prompt { get; set; }

    [JsonPropertyName("v4_negative_prompt")]
    public V4ConditionInput? V4NegativePrompt { get; set; }
}

public class Img2ImgParameters
{
    public double strength { get; set; } = 0.7;
    public double? begin_from_sigma { get; set; }
    public double noise { get; set; }
    public long? extra_noise_seed { get; set; }
    public bool color_correct { get; set; } = true;
}

public class DirectorReferenceDescription
{
    [JsonPropertyName("caption")]
    public V4ExternalCaption Caption { get; set; } = new V4ExternalCaption();

    [JsonPropertyName("use_coords")]
    public bool UseCoords { get; set; }

    [JsonPropertyName("use_order")]
    public bool UseOrder { get; set; }

    [JsonPropertyName("legacy_uc")]
    public bool LegacyUc { get; set; }
}

public class V4ConditionInput
{
    [JsonPropertyName("caption")]
    public V4ExternalCaption Caption { get; set; } = new V4ExternalCaption();

    /*[JsonPropertyName("legacy_uc")]
    public bool LegacyUc { get; set; } = false;*/

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
    public double x { get; set; } = 0.5;
    public double y { get; set; } = 0.5;
}
