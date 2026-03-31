using System.Net.Http.Json;
using AgriPredict.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgriPredict.DataIngestion.OpenMeteo;

/// <summary>
/// Fetches daily historical weather from the Open-Meteo archive API.
/// No API key required — free public endpoint.
/// </summary>
public sealed class OpenMeteoClient
{
    private const string BaseUrl = "https://archive-api.open-meteo.com/v1/archive";

    private readonly HttpClient _http;
    private readonly ILogger<OpenMeteoClient> _logger;

    public OpenMeteoClient(HttpClient http, ILogger<OpenMeteoClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    /// <summary>
    /// Fetches daily observations for the given location and date range.
    /// Rows with any null value are dropped (data-quality guard).
    /// </summary>
    public async Task<IReadOnlyList<WeatherObservation>> FetchHistoricalAsync(
        double latitude,
        double longitude,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default)
    {
        var url = BuildUrl(latitude, longitude, startDate, endDate);

        _logger.LogInformation(
            "[OpenMeteoClient] Requesting data lat={Lat} lon={Lon} from {Start} to {End}",
            latitude, longitude, startDate, endDate);

        var response = await _http.GetFromJsonAsync<OpenMeteoResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Open-Meteo returned a null response body.");

        var daily = response.Daily;
        var count = daily.Time.Count;

        _logger.LogInformation("[OpenMeteoClient] Received {Count} daily rows", count);

        var observations = new List<WeatherObservation>(count);

        for (var i = 0; i < count; i++)
        {
            var tempMin = daily.Temperature2mMin[i];
            var tempMax = daily.Temperature2mMax[i];
            var precip  = daily.PrecipitationSum[i];
            var wind    = daily.Windspeed10mMax[i];

            if (tempMin is null || tempMax is null || precip is null || wind is null)
            {
                _logger.LogDebug("[OpenMeteoClient] Skipping row {Date} — null value(s)", daily.Time[i]);
                continue;
            }

            observations.Add(new WeatherObservation
            {
                Date          = DateOnly.Parse(daily.Time[i]),
                TempMin       = tempMin.Value,
                TempMax       = tempMax.Value,
                Precipitation = precip.Value,
                WindSpeed     = wind.Value,
            });
        }

        _logger.LogInformation("[OpenMeteoClient] {Valid} valid rows after null-filtering", observations.Count);
        return observations;
    }

    /// <summary>
    /// Fetches daily forecast data from the Open-Meteo forecast API.
    /// Uses timezone=auto so the response aligns with local solar time at the given coordinates.
    /// </summary>
    public async Task<IReadOnlyList<WeatherObservation>> FetchForecastAsync(
        double latitude,
        double longitude,
        int forecastDays = 7,
        CancellationToken cancellationToken = default)
    {
        var url = BuildForecastUrl(latitude, longitude, forecastDays);

        _logger.LogInformation(
            "[OpenMeteoClient] Requesting {Days}-day forecast lat={Lat} lon={Lon}",
            forecastDays, latitude, longitude);

        var response = await _http.GetFromJsonAsync<OpenMeteoForecastResponse>(url, cancellationToken)
            ?? throw new InvalidOperationException("Open-Meteo forecast returned a null response body.");

        var daily = response.Daily;
        var count = daily.Time.Count;

        _logger.LogInformation("[OpenMeteoClient] Forecast received {Count} daily rows", count);

        var observations = new List<WeatherObservation>(count);

        for (var i = 0; i < count; i++)
        {
            var tempMin = daily.Temperature2mMin[i];
            var tempMax = daily.Temperature2mMax[i];
            var precip  = daily.PrecipitationSum[i];
            var wind    = daily.WindSpeed10mMax[i];

            if (tempMin is null || tempMax is null || precip is null || wind is null)
            {
                _logger.LogDebug("[OpenMeteoClient] Skipping forecast row {Date} — null value(s)", daily.Time[i]);
                continue;
            }

            observations.Add(new WeatherObservation
            {
                Date          = DateOnly.Parse(daily.Time[i]),
                TempMin       = tempMin.Value,
                TempMax       = tempMax.Value,
                Precipitation = precip.Value,
                WindSpeed     = wind.Value,
            });
        }

        _logger.LogInformation("[OpenMeteoClient] {Valid} valid forecast rows after null-filtering", observations.Count);
        return observations;
    }

    private static string BuildUrl(double lat, double lon, DateOnly start, DateOnly end) =>
        $"{BaseUrl}?latitude={lat}&longitude={lon}" +
        $"&start_date={start:yyyy-MM-dd}&end_date={end:yyyy-MM-dd}" +
        "&daily=temperature_2m_min,temperature_2m_max,precipitation_sum,windspeed_10m_max" +
        "&timezone=Europe%2FStockholm";

    // NOTE: forecast endpoint uses wind_speed_10m_max (with underscore) — different from archive
    private static string BuildForecastUrl(double lat, double lon, int forecastDays) =>
        $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}" +
        "&daily=temperature_2m_min,temperature_2m_max,precipitation_sum,wind_speed_10m_max" +
        $"&forecast_days={forecastDays}&timezone=auto";
}
