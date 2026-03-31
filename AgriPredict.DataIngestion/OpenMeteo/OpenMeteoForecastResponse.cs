using System.Text.Json.Serialization;

namespace AgriPredict.DataIngestion.OpenMeteo;

/// <summary>
/// JSON response from the Open-Meteo forecast endpoint (api.open-meteo.com/v1/forecast).
/// NOTE: The forecast API uses "wind_speed_10m_max" (underscore between wind/speed),
/// which differs from the archive API's "windspeed_10m_max" — separate DTOs are required.
/// </summary>
internal sealed class OpenMeteoForecastResponse
{
    [JsonPropertyName("daily")]
    public ForecastDailyData Daily { get; init; } = new();
}

internal sealed class ForecastDailyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; init; } = [];

    [JsonPropertyName("temperature_2m_min")]
    public List<float?> Temperature2mMin { get; init; } = [];

    [JsonPropertyName("temperature_2m_max")]
    public List<float?> Temperature2mMax { get; init; } = [];

    [JsonPropertyName("precipitation_sum")]
    public List<float?> PrecipitationSum { get; init; } = [];

    /// <summary>
    /// Forecast API field name is "wind_speed_10m_max" — different from archive's "windspeed_10m_max".
    /// </summary>
    [JsonPropertyName("wind_speed_10m_max")]
    public List<float?> WindSpeed10mMax { get; init; } = [];
}
