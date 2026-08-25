using System.Text.Json.Serialization;

namespace ImageGen.Models.Api;

public class UserDataResponse
{
    [JsonPropertyName("subscription")]
    public SubscriptionData? Subscription { get; set; }
}

public class SubscriptionData
{
    [JsonPropertyName("trainingStepsLeft")]
    public TrainingStepsLeft? TrainingStepsLeft { get; set; }

    [JsonPropertyName("usage")]
    public OpusUsageData? Usage { get; set; }
}

public class OpusUsageData
{
    [JsonPropertyName("percent")]
    public int Percent { get; set; }

    [JsonPropertyName("isNegative")]
    public bool IsNegative { get; set; }

    [JsonPropertyName("timeUntilNextPercent")]
    public int? TimeUntilNextPercent { get; set; }
}

public class TrainingStepsLeft
{
    [JsonPropertyName("fixedTrainingStepsLeft")]
    public int FixedTrainingStepsLeft { get; set; }

    [JsonPropertyName("purchasedTrainingSteps")]
    public int PurchasedTrainingSteps { get; set; }
}
