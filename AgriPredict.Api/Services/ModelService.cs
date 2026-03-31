using AgriPredict.Api.Models;
using AgriPredict.Training;
using Microsoft.ML;

namespace AgriPredict.Api.Services;

/// <summary>
/// Loads the trained FastForest model once at startup and exposes prediction.
/// <see cref="PredictionEngine{TSrc,TDst}"/> is not thread-safe — access is serialised with a lock.
/// </summary>
public sealed class ModelService : IModelService
{
    private readonly PredictionEngine<FrostRiskInput, FrostRiskPrediction>? _engine;
    private readonly ILogger<ModelService> _logger;
    private readonly object _lock = new();

    public bool IsAvailable { get; }

    public ModelService(IConfiguration config, ILogger<ModelService> logger)
    {
        _logger = logger;

        var modelPath = config["DataPaths:Model"] ?? "data/model.zip";

        if (!File.Exists(modelPath))
        {
            _logger.LogError(
                "[ModelService] model.zip not found at '{Path}'. " +
                "Run POST /api/v1/ingest then POST /api/v1/train to generate it. " +
                "All /predict endpoints will return HTTP 503 until the model is loaded.",
                modelPath);
            IsAvailable = false;
            return;
        }

        try
        {
            var mlContext = new MLContext();
            var model = mlContext.Model.Load(modelPath, out _);
            _engine = mlContext.Model.CreatePredictionEngine<FrostRiskInput, FrostRiskPrediction>(model);
            IsAvailable = true;
            _logger.LogInformation("[ModelService] Model loaded successfully from '{Path}'", modelPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ModelService] Failed to load model from '{Path}'", modelPath);
            IsAvailable = false;
        }
    }

    public FrostRiskPrediction PredictFrostRisk(
        float tempMin,
        float tempMax,
        float precipitation,
        float windSpeed,
        int dayOfYear)
    {
        if (_engine is null)
            throw new InvalidOperationException("Model is not available. Check startup logs.");

        var input = new FrostRiskInput
        {
            TempMin       = tempMin,
            TempMax       = tempMax,
            Precipitation = precipitation,
            WindSpeed     = windSpeed,
            DayOfYear     = (float)dayOfYear,
        };

        lock (_lock)
        {
            return _engine.Predict(input);
        }
    }
}
