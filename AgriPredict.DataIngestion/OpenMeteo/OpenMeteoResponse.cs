using System.Text.Json.Serialization;

namespace AgriPredict.DataIngestion.OpenMeteo;

/// <summary>Top-level JSON response from the Open-Meteo archive endpoint.</summary>
internal sealed class OpenMeteoResponse
{
    [JsonPropertyName("daily")]
    public DailyData Daily { get; init; } = new();
}

internal sealed class DailyData
{
    [JsonPropertyName("time")]
    public List<string> Time { get; init; } = [];

    [JsonPropertyName("temperature_2m_min")]
    public List<float?> Temperature2mMin { get; init; } = [];

    [JsonPropertyName("temperature_2m_max")]
    public List<float?> Temperature2mMax { get; init; } = [];

    [JsonPropertyName("precipitation_sum")]
    public List<float?> PrecipitationSum { get; init; } = [];

    [JsonPropertyName("windspeed_10m_max")]
    public List<float?> Windspeed10mMax { get; init; } = [];
}
