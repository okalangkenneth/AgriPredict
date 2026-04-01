using AgriPredict.Api.Models;
using AgriPredict.Api.Services;
using AgriPredict.Api.Validators;
using AgriPredict.DataIngestion.OpenMeteo;
using AgriPredict.DataIngestion.Persistence;
using AgriPredict.Training;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;
using Serilog;

// ── Serilog — configure before host builds so startup errors are captured ─────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/agripredict-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog();

    // ── Services ──────────────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title       = "AgriPredict API",
            Version     = "v1",
            Description = "Frost risk and rainfall prediction for precision agriculture. " +
                          "Run POST /api/v1/ingest → POST /api/v1/train before calling /predict endpoints.",
        });
    });

    builder.Services.AddProblemDetails();
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader());
    });
    builder.Services.AddHttpClient<OpenMeteoClient>();

    builder.Services.AddSingleton(sp =>
    {
        var logger   = sp.GetRequiredService<ILogger<WeatherDataStore>>();
        var dataPath = builder.Configuration["DataPaths:WeatherHistory"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "weather-history.json");
        return new WeatherDataStore(dataPath, logger);
    });

    builder.Services.AddSingleton<IModelService, ModelService>();

    // ── Build ─────────────────────────────────────────────────────────────────
    var app = builder.Build();

    // Eagerly resolve ModelService so startup error (missing model.zip) is logged immediately.
    app.Services.GetRequiredService<IModelService>();

    // ── Swagger ───────────────────────────────────────────────────────────────
    app.UseCors();
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "AgriPredict v1"));

    // ── Helpers ───────────────────────────────────────────────────────────────
    static string FrostRiskLabel(float probability) => probability switch
    {
        < 0.35f  => "Low",
        <= 0.65f => "Medium",
        _        => "High",
    };

    static IResult ValidationProblem(IEnumerable<FluentValidation.Results.ValidationFailure> errors) =>
        Results.Problem(
            title:      "Validation failed",
            detail:     string.Join("; ", errors.Select(e => e.ErrorMessage)),
            statusCode: StatusCodes.Status400BadRequest);

    static IResult ModelUnavailable() =>
        Results.Problem(
            title:      "Model not available",
            detail:     "The ML model has not been trained yet. " +
                        "POST /api/v1/ingest then POST /api/v1/train to generate data/model.zip.",
            statusCode: StatusCodes.Status503ServiceUnavailable);

    // ── Health ────────────────────────────────────────────────────────────────
    app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
       .WithName("Health")
       .WithSummary("Health check");

    // ── Data ingestion ────────────────────────────────────────────────────────
    app.MapPost("/api/v1/ingest", async (
        OpenMeteoClient client,
        WeatherDataStore store,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        logger.LogInformation("[Ingest] Starting historical data fetch for Uppsala, SE");

        var observations = await client.FetchHistoricalAsync(
            latitude:          59.86,
            longitude:         17.64,
            startDate:         new DateOnly(2020, 1, 1),
            endDate:           new DateOnly(2024, 12, 31),
            cancellationToken: ct);

        await store.SaveAsync(observations, ct);
        logger.LogInformation("[Ingest] Completed — {Count} records persisted", observations.Count);
        return Results.Ok(new { recordsIngested = observations.Count });
    })
    .WithName("IngestWeatherData")
    .WithSummary("Fetch and persist historical weather data for Uppsala, SE (2020–2024)");

    // ── Training ──────────────────────────────────────────────────────────────
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

        return Results.Ok(new { message = "Training complete. Model saved to data/model.zip. Restart the API to load the new model." });
    })
    .WithName("TrainModel")
    .WithSummary("Train the FrostRisk ML model and save to data/model.zip (requires prior ingest)");

    // ── Frost prediction ──────────────────────────────────────────────────────
    app.MapGet("/api/v1/predict/frost", async (
        double lat,
        double lon,
        IModelService modelService,
        OpenMeteoClient forecastClient,
        IConfiguration config,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var coordResult = new CoordinateValidator().Validate(new CoordinateRequest(lat, lon));
        if (!coordResult.IsValid)
            return ValidationProblem(coordResult.Errors);

        if (!modelService.IsAvailable)
            return ModelUnavailable();

        var forecast = await forecastClient.FetchForecastAsync(lat, lon, forecastDays: 3, cancellationToken: ct);
        if (forecast.Count == 0)
            return Results.Problem(
                title:      "No forecast data",
                detail:     "Open-Meteo returned no forecast rows for the requested location.",
                statusCode: StatusCodes.Status502BadGateway);

        var today      = forecast[0];
        var prediction = modelService.PredictFrostRisk(
            today.TempMin, today.TempMax, today.Precipitation, today.WindSpeed, today.DayOfYear);

        var modelVersion = config["ModelVersion"] ?? "1.0.0";
        logger.LogInformation(
            "[Predict/Frost] lat={Lat} lon={Lon} → probability={Prob:P1} label={Label}",
            lat, lon, prediction.Probability, FrostRiskLabel(prediction.Probability));

        return Results.Ok(new PredictFrostResponse
        {
            Location             = new LocationDto(lat, lon),
            FrostRiskProbability = MathF.Round(prediction.Probability, 4),
            FrostRiskLabel       = FrostRiskLabel(prediction.Probability),
            ForecastWindowHours  = 48,
            ModelVersion         = modelVersion,
            GeneratedAt          = DateTimeOffset.UtcNow,
        });
    })
    .WithName("PredictFrost")
    .WithSummary("Predict 48-hour frost risk for a given location");

    // ── Rainfall prediction ───────────────────────────────────────────────────
    app.MapGet("/api/v1/predict/rainfall", async (
        double lat,
        double lon,
        int days,
        IModelService modelService,
        OpenMeteoClient forecastClient,
        IConfiguration config,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var requestResult = new RainfallRequestValidator().Validate(new RainfallRequest(lat, lon, days));
        if (!requestResult.IsValid)
            return ValidationProblem(requestResult.Errors);

        if (!modelService.IsAvailable)
            return ModelUnavailable();

        // Fetch one extra day so edge rows have look-ahead context
        var forecast = await forecastClient.FetchForecastAsync(lat, lon, forecastDays: days + 1, cancellationToken: ct);
        if (forecast.Count == 0)
            return Results.Problem(
                title:      "No forecast data",
                detail:     "Open-Meteo returned no forecast rows for the requested location.",
                statusCode: StatusCodes.Status502BadGateway);

        var availableDays = Math.Min(days, forecast.Count);
        var probabilities = new float[availableDays];

        for (var i = 0; i < availableDays; i++)
        {
            var day = forecast[i];
            var pred = modelService.PredictFrostRisk(
                day.TempMin, day.TempMax, day.Precipitation, day.WindSpeed, day.DayOfYear);
            probabilities[i] = MathF.Round(pred.Probability, 4);
        }

        var modelVersion = config["ModelVersion"] ?? "1.0.0";
        logger.LogInformation(
            "[Predict/Rainfall] lat={Lat} lon={Lon} days={Days} → [{Probs}]",
            lat, lon, days, string.Join(", ", probabilities.Select(p => p.ToString("F4"))));

        return Results.Ok(new PredictRainfallResponse
        {
            Location                  = new LocationDto(lat, lon),
            RainfallProbabilityByDay  = probabilities,
            ForecastDays              = availableDays,
            ModelVersion              = modelVersion,
            GeneratedAt               = DateTimeOffset.UtcNow,
        });
    })
    .WithName("PredictRainfall")
    .WithSummary("Predict rainfall probability for 1–7 days at a given location (uses frost-risk model as proxy)");

    // ── Current weather ───────────────────────────────────────────────────────
    app.MapGet("/api/v1/weather/current", async (
        double lat,
        double lon,
        OpenMeteoClient forecastClient,
        ILogger<Program> logger,
        CancellationToken ct) =>
    {
        var coordResult = new CoordinateValidator().Validate(new CoordinateRequest(lat, lon));
        if (!coordResult.IsValid)
            return ValidationProblem(coordResult.Errors);

        var forecast = await forecastClient.FetchForecastAsync(lat, lon, forecastDays: 1, cancellationToken: ct);
        if (forecast.Count == 0)
            return Results.Problem(
                title:      "No weather data",
                detail:     "Open-Meteo returned no forecast rows for the requested location.",
                statusCode: StatusCodes.Status502BadGateway);

        logger.LogInformation("[Weather/Current] lat={Lat} lon={Lon} date={Date}", lat, lon, forecast[0].Date);
        return Results.Ok(forecast[0]);
    })
    .WithName("CurrentWeather")
    .WithSummary("Return today's raw weather observation for a given location (no ML inference)");

    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "AgriPredict API terminated unexpectedly");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
