namespace AgriPredict.Api.Models;

public sealed class PredictFrostResponse
{
    public LocationDto Location { get; init; } = new(0, 0);

    /// <summary>Model probability (0–1), rounded to 4 decimal places.</summary>
    public float FrostRiskProbability { get; init; }

    /// <summary>"Low" (&lt;0.35) | "Medium" (0.35–0.65) | "High" (&gt;0.65)</summary>
    public string FrostRiskLabel { get; init; } = string.Empty;

    /// <summary>Prediction look-ahead window in hours (always 48 for this model).</summary>
    public int ForecastWindowHours { get; init; } = 48;

    public string ModelVersion { get; init; } = string.Empty;

    public DateTimeOffset GeneratedAt { get; init; }
}
