using Microsoft.ML.Data;

namespace AgriPredict.Api.Models;

/// <summary>
/// ML.NET output schema for the FrostRisk binary classifier.
/// Column names must match the trainer's output column names exactly.
/// </summary>
public sealed class FrostRiskPrediction
{
    [ColumnName("PredictedLabel")]
    public bool PredictedLabel { get; set; }

    [ColumnName("Probability")]
    public float Probability { get; set; }

    [ColumnName("Score")]
    public float Score { get; set; }
}
