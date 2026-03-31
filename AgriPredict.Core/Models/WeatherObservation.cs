namespace AgriPredict.Core.Models;

/// <summary>
/// A single daily weather observation as returned by Open-Meteo archive API.
/// </summary>
public sealed class WeatherObservation
{
    public DateOnly Date { get; init; }

    /// <summary>Minimum 2 m air temperature for the day (°C).</summary>
    public float TempMin { get; init; }

    /// <summary>Maximum 2 m air temperature for the day (°C).</summary>
    public float TempMax { get; init; }

    /// <summary>Total precipitation for the day (mm).</summary>
    public float Precipitation { get; init; }

    /// <summary>Maximum 10 m wind speed for the day (km/h).</summary>
    public float WindSpeed { get; init; }

    /// <summary>Day-of-year (1–366) — seasonality proxy for ML feature.</summary>
    public int DayOfYear => Date.DayOfYear;
}
