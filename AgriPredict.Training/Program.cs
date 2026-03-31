using AgriPredict.Training;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));

var logger = loggerFactory.CreateLogger("AgriPredict.Training");

// ── Resolve paths ────────────────────────────────────────────────────────────
// Default: look for data/ relative to the solution root (two levels up from bin/).
// Override with environment variables for CI / Docker scenarios.
var solutionRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

var weatherDataPath = Environment.GetEnvironmentVariable("AGRIPREDICT_WEATHER_DATA")
    ?? Path.Combine(solutionRoot, "data", "weather-history.json");

var modelOutputPath = Environment.GetEnvironmentVariable("AGRIPREDICT_MODEL_PATH")
    ?? Path.Combine(solutionRoot, "data", "model.zip");

logger.LogInformation("[Training] Weather data : {Path}", weatherDataPath);
logger.LogInformation("[Training] Model output : {Path}", modelOutputPath);

await FrostRiskTrainer.RunAsync(weatherDataPath, modelOutputPath, logger);

logger.LogInformation("[Training] Done.");
