using AgriPredict.DataIngestion.OpenMeteo;
using AgriPredict.DataIngestion.Persistence;
using AgriPredict.Training;

var builder = WebApplication.CreateBuilder(args);

// ── HTTP client for Open-Meteo ────────────────────────────────────────────────
builder.Services.AddHttpClient<OpenMeteoClient>();

// ── Weather data store (file path from config, defaulting to data/ folder) ────
builder.Services.AddSingleton(sp =>
{
    var logger = sp.GetRequiredService<ILogger<WeatherDataStore>>();
    var dataPath = builder.Configuration["DataPaths:WeatherHistory"]
        ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "weather-history.json");
    return new WeatherDataStore(dataPath, logger);
});

var app = builder.Build();

// ── Health ────────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .WithName("Health");

// ── Data ingestion trigger (Phase 1 — dev convenience endpoint) ───────────────
app.MapPost("/api/v1/ingest", async (
    OpenMeteoClient client,
    WeatherDataStore store,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    logger.LogInformation("[Ingest] Starting historical data fetch for Uppsala, SE");

    var observations = await client.FetchHistoricalAsync(
        latitude:  59.86,
        longitude: 17.64,
        startDate: new DateOnly(2020, 1, 1),
        endDate:   new DateOnly(2024, 12, 31),
        cancellationToken: ct);

    await store.SaveAsync(observations, ct);

    logger.LogInformation("[Ingest] Completed — {Count} records persisted", observations.Count);
    return Results.Ok(new { recordsIngested = observations.Count });
})
.WithName("IngestWeatherData");

// ── Training trigger (Phase 2 — trains model and saves data/model.zip) ────────
app.MapPost("/api/v1/train", async (
    IConfiguration config,
    ILogger<Program> logger,
    CancellationToken ct) =>
{
    var weatherPath = config["DataPaths:WeatherHistory"]
        ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "weather-history.json");
    var modelPath = config["DataPaths:Model"]
        ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "model.zip");

    logger.LogInformation("[Train] Starting training run");
    await FrostRiskTrainer.RunAsync(weatherPath, modelPath, logger, ct);
    logger.LogInformation("[Train] Training run complete");

    return Results.Ok(new { message = "Training complete. Model saved to data/model.zip." });
})
.WithName("TrainModel");

app.Run();
