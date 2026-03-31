namespace AgriPredict.Core.Models;

/// <summary>
/// Training row: a WeatherObservation enriched with the supervised frost-risk label.
/// Label = true when TempMin ≤ 0 °C within the next 2 calendar days.
/// </summary>
public sealed class FrostRiskLabel
{
    public DateOnly Date { get; init; }
    public float TempMin { get; init; }
    public float TempMax { get; init; }
    public float Precipitation { get; init; }
    public float WindSpeed { get; init; }
    public int DayOfYear { get; init; }

    /// <summary>True when frost (TempMin ≤ 0 °C) occurs within the next 48 h.</summary>
    public bool FrostRisk { get; init; }
}
