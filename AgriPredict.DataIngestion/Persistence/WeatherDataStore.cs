using System.Text.Json;
using System.Text.Json.Serialization;
using AgriPredict.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgriPredict.DataIngestion.Persistence;

/// <summary>
/// Reads and writes <see cref="WeatherObservation"/> records to a JSON file on disk.
/// </summary>
public sealed class WeatherDataStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;
    private readonly ILogger<WeatherDataStore> _logger;

    public WeatherDataStore(string filePath, ILogger<WeatherDataStore> logger)
    {
        _filePath = filePath;
        _logger = logger;
    }

    /// <summary>Persists observations to <see cref="_filePath"/>, overwriting any existing file.</summary>
    public async Task SaveAsync(
        IReadOnlyList<WeatherObservation> observations,
        CancellationToken cancellationToken = default)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        await using var stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, observations, JsonOptions, cancellationToken);

        _logger.LogInformation(
            "[WeatherDataStore] Saved {Count} observations to {Path}",
            observations.Count, _filePath);
    }

    /// <summary>Loads observations from <see cref="_filePath"/>.</summary>
    public async Task<IReadOnlyList<WeatherObservation>> LoadAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            _logger.LogWarning("[WeatherDataStore] File not found: {Path}", _filePath);
            return [];
        }

        await using var stream = File.OpenRead(_filePath);
        var result = await JsonSerializer.DeserializeAsync<List<WeatherObservation>>(
            stream, JsonOptions, cancellationToken);

        _logger.LogInformation(
            "[WeatherDataStore] Loaded {Count} observations from {Path}",
            result?.Count ?? 0, _filePath);

        return result ?? [];
    }
}
