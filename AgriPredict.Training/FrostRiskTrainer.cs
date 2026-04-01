using AgriPredict.DataIngestion.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.ML;

namespace AgriPredict.Training;

/// <summary>
/// Trains a FastForest binary classifier to predict frost risk (TempMin ≤ 0 °C
/// within the next 2 calendar days) and serialises the model to disk.
/// </summary>
public static class FrostRiskTrainer
{
    private static readonly string[] FeatureColumns =
    [
        nameof(FrostRiskInput.TempMin),
        nameof(FrostRiskInput.TempMax),
        nameof(FrostRiskInput.Precipitation),
        nameof(FrostRiskInput.WindSpeed),
        nameof(FrostRiskInput.DayOfYear),
    ];

    /// <summary>
    /// Full training run: load → label → split → train → evaluate → save.
    /// </summary>
    /// <param name="weatherDataPath">Path to weather-history.json produced by ingestion.</param>
    /// <param name="modelOutputPath">Destination path for the serialised model.zip.</param>
    /// <param name="logger">Logger for progress and metrics output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task RunAsync(
        string weatherDataPath,
        string modelOutputPath,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        logger.LogInformation("[FrostRiskTrainer] Loading weather data from {Path}", weatherDataPath);

        var dataStoreLogger = LoggerFactory
            .Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Warning))
            .CreateLogger<WeatherDataStore>();

        var store = new WeatherDataStore(weatherDataPath, dataStoreLogger);
        var observations = await store.LoadAsync(cancellationToken);

        if (observations.Count < 10)
        {
            logger.LogError(
                "[FrostRiskTrainer] Insufficient data ({Count} rows). Run /api/v1/ingest first.",
                observations.Count);
            return;
        }

        // ── Build labelled rows ──────────────────────────────────────────────
        // Label: frost risk = true when the minimum temperature on day N+1 or N+2 ≤ 0 °C.
        // We stop at Count-2 so look-ahead indices [i+1] and [i+2] are always valid.
        var sorted = observations.OrderBy(o => o.Date).ToList();
        var inputs = new List<FrostRiskInput>(sorted.Count - 2);

        for (var i = 0; i < sorted.Count - 2; i++)
        {
            var obs = sorted[i];
            var frostAhead = sorted[i + 1].TempMin <= 0f || sorted[i + 2].TempMin <= 0f;

            inputs.Add(new FrostRiskInput
            {
                TempMin       = obs.TempMin,
                TempMax       = obs.TempMax,
                Precipitation = obs.Precipitation,
                WindSpeed     = obs.WindSpeed,
                DayOfYear     = (float)obs.DayOfYear,
                FrostRisk     = frostAhead,
            });
        }

        var frostCount = inputs.Count(r => r.FrostRisk);
        logger.LogInformation(
            "[FrostRiskTrainer] {Total} labelled rows — {Frost} frost ({FrostPct:P1}), {NoFrost} no-frost",
            inputs.Count, frostCount,
            (double)frostCount / inputs.Count,
            inputs.Count - frostCount);

        // ── ML.NET pipeline ──────────────────────────────────────────────────
        // Seed on MLContext makes every random operation reproducible (split, forest).
        var mlContext = new MLContext(seed: 42);

        var data = mlContext.Data.LoadFromEnumerable(inputs);

        // 80 % train / 20 % test — fixed seed so split is reproducible.
        var split = mlContext.Data.TrainTestSplit(data, testFraction: 0.2, seed: 42);

        var pipeline = mlContext.Transforms
            .Concatenate("Features", FeatureColumns)
            .AppendCacheCheckpoint(mlContext)
            .Append(mlContext.BinaryClassification.Trainers.FastForest(
                labelColumnName:  "Label",
                featureColumnName: "Features"));

        logger.LogInformation("[FrostRiskTrainer] Training FastForest model…");
        var model = pipeline.Fit(split.TrainSet);

        // ── Save model (before evaluation — ensures model.zip is always written) ──
        var outputDir = Path.GetDirectoryName(modelOutputPath);
        if (!string.IsNullOrEmpty(outputDir))
            Directory.CreateDirectory(outputDir);

        mlContext.Model.Save(model, data.Schema, modelOutputPath);
        logger.LogInformation("[FrostRiskTrainer] Model saved to {Path}", modelOutputPath);

        // ── Evaluation ───────────────────────────────────────────────────────
        try
        {
            var predictions = model.Transform(split.TestSet);
            var metrics = mlContext.BinaryClassification.Evaluate(
                predictions,
                labelColumnName:          "Label",
                scoreColumnName:          "Score",
                probabilityColumnName:    "Probability",
                predictedLabelColumnName: "PredictedLabel");

            logger.LogInformation(
                "[FrostRiskTrainer] ── Evaluation on test split ({TestRows} rows) ──",
                split.TestSet.GetRowCount() ?? 0);
            logger.LogInformation("[FrostRiskTrainer]   Accuracy : {Accuracy:P2}", metrics.Accuracy);
            logger.LogInformation("[FrostRiskTrainer]   AUC      : {AUC:F4}",      metrics.AreaUnderRocCurve);
            logger.LogInformation("[FrostRiskTrainer]   F1       : {F1:F4}",       metrics.F1Score);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[FrostRiskTrainer] Evaluation failed — model saved successfully, metrics unavailable");
        }
    }
}
