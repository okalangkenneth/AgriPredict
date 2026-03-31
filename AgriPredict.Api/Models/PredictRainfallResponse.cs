namespace AgriPredict.Api.Models;

public sealed class PredictRainfallResponse
{
    public LocationDto Location { get; init; } = new(0, 0);

    /// <summary>One probability entry per requested day (0–1, 4 decimal places).</summary>
    public float[] RainfallProbabilityByDay { get; init; } = [];

    public int ForecastDays { get; init; }

    public string ModelVersion { get; init; } = string.Empty;

    /// <summary>Transparent note that rainfall uses the frost-risk model as a proxy.</summary>
    public string ModelNote { get; init; } =
        "Rainfall prediction uses frost-risk model as proxy. A dedicated rainfall model is planned for a future release.";

    public DateTimeOffset GeneratedAt { get; init; }
}
