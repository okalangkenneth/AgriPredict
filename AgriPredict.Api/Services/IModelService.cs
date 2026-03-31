using AgriPredict.Api.Models;

namespace AgriPredict.Api.Services;

public interface IModelService
{
    /// <summary>True when model.zip was loaded successfully at startup.</summary>
    bool IsAvailable { get; }

    FrostRiskPrediction PredictFrostRisk(
        float tempMin,
        float tempMax,
        float precipitation,
        float windSpeed,
        int dayOfYear);
}
