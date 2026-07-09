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
}

public class TrainingStepsLeft
{
    [JsonPropertyName("fixedTrainingStepsLeft")]
    public int FixedTrainingStepsLeft { get; set; }

    [JsonPropertyName("purchasedTrainingSteps")]
    public int PurchasedTrainingSteps { get; set; }
}
