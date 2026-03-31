# AgriPredict — CLAUDE.md

## Project Overview

**Name:** AgriPredict  
**Type:** Greenfield — .NET 8 Predictive Analytics API  
**Domain:** Precision Agriculture / Weather Prediction  
**Goal:** A production-aware .NET 8 microservice that uses ML.NET to predict frost risk and rainfall probability for a given agricultural location, trained on open weather data.

**Portfolio purpose:** Demonstrates ML integration in .NET, domain-driven design from an agriculture background, clean REST API design, and production awareness (retraining pipelines, observability, containerisation).

**GitHub:** https://github.com/okalangkenneth/AgriPredict  
**Live Demo:** GitHub Pages (map-based UI, pick location → get forecast)

---

## Memory Architecture (3-Layer System)

| Layer | Location | Purpose | Auto-Loaded |
|-------|----------|---------|-------------|
| **CLAUDE.md** | Project root | Rules, workflow, conventions | ✅ Always |
| **MEMORY.md** | `~/.claude/projects/<project>/memory/` | Session learnings, patterns Claude discovers | ✅ First 200 lines |
| **claude-mem** | `~/.claude-mem/` | Deep searchable history, AI-compressed | ✅ Via MCP injection |

---

## Anti-Hallucination Protocol

```
API / library question?       → Query docs FIRST, then answer
Recent facts / news?          → WebSearch FIRST
File content question?        → Read the file FIRST
Uncertain about anything?     → Say "I need to verify" and use tools

NEVER:
  - Invent ML.NET API signatures
  - Guess NuGet package versions
  - Assume Open-Meteo API response shapes without verification
  - Fabricate training accuracy figures
```

### Mandatory Confidence Levels

| Level | Meaning | Required Action |
|-------|---------|----------------|
| **HIGH** | Verified against docs/source | Can proceed |
| **MEDIUM** | Based on training, not verified | Flag it; verify before production use |
| **LOW** | Uncertain, educated guess | Must verify before any use |
| **UNKNOWN** | Cannot determine | State explicitly, do not guess |

---

## Build Progress (KEEP UPDATED)

**Claude Code: Update this section at the end of every session.**

### ✅ COMPLETED
- [x] Phase 1 — Project scaffold + Open-Meteo data ingestion (2026-03-31)
  - `AgriPredict.sln` — 3-project solution
  - `AgriPredict.Core/Models/WeatherObservation.cs` — daily weather domain model
  - `AgriPredict.Core/Models/FrostRiskLabel.cs` — labelled training record
  - `AgriPredict.DataIngestion/OpenMeteo/OpenMeteoClient.cs` — archive API client
  - `AgriPredict.DataIngestion/OpenMeteo/OpenMeteoResponse.cs` — JSON DTOs
  - `AgriPredict.DataIngestion/Persistence/WeatherDataStore.cs` — JSON file store
  - `AgriPredict.Api/Program.cs` — minimal API: `/health` + `POST /api/v1/ingest`
  - `AgriPredict.Api/appsettings.json` — DataPaths config
  - `.gitignore` — excludes model.zip, weather-history.json, secrets
  - `dotnet build` → 0 errors, 0 warnings ✅

- [x] Phase 2 — ML.NET FrostRisk training pipeline (2026-03-31)
  - `AgriPredict.Training/AgriPredict.Training.csproj` — console Exe; refs Core + DataIngestion
  - `AgriPredict.Training/FrostRiskInput.cs` — ML.NET input type (`[ColumnName("Label")]` on bool)
  - `AgriPredict.Training/FrostRiskTrainer.cs` — load → label (N+1/N+2 look-ahead) → FastForest (seed 42, 80/20 split) → evaluate (Accuracy/AUC/F1) → save model.zip
  - `AgriPredict.Training/Program.cs` — console entry point; paths from env vars or defaults
  - `AgriPredict.Api/Program.cs` — added `POST /api/v1/train` endpoint
  - NuGet: `Microsoft.ML 5.0.0`, `Microsoft.ML.FastTree 5.0.0`, `Microsoft.Extensions.Logging.Console`
  - `dotnet build` → 0 errors, 0 warnings ✅

- [x] Phase 3 — Prediction REST API with Swagger, Serilog, FluentValidation (2026-03-31)
  - `AgriPredict.Api/Models/FrostRiskPrediction.cs` — ML.NET output schema (`PredictedLabel`, `Probability`, `Score`)
  - `AgriPredict.Api/Models/PredictFrostResponse.cs` — frost endpoint response shape
  - `AgriPredict.Api/Models/PredictRainfallResponse.cs` — rainfall endpoint response shape + proxy model note
  - `AgriPredict.Api/Models/LocationDto.cs` — shared location record
  - `AgriPredict.Api/Services/IModelService.cs` — `IsAvailable` + `PredictFrostRisk()` interface
  - `AgriPredict.Api/Services/ModelService.cs` — loads model.zip once; thread-safe lock; 503 guard
  - `AgriPredict.Api/Validators/CoordinateValidator.cs` — FluentValidation lat/lon (-90..90, -180..180)
  - `AgriPredict.Api/Validators/RainfallRequestValidator.cs` — FluentValidation lat/lon + days (1..7)
  - `AgriPredict.DataIngestion/OpenMeteo/OpenMeteoForecastResponse.cs` — separate forecast DTOs (wind_speed_10m_max ≠ archive windspeed_10m_max)
  - `AgriPredict.DataIngestion/OpenMeteo/OpenMeteoClient.cs` — added `FetchForecastAsync()` + `BuildForecastUrl()`
  - `AgriPredict.Training/FrostRiskInput.cs` — changed `internal` → `public` (needed by ModelService)
  - `AgriPredict.Api/Program.cs` — full rewrite: Serilog, Swagger (Swashbuckle 10.x / Microsoft.OpenApi 2.x namespace), 6 endpoints, ProblemDetails 400/503
  - `AgriPredict.Api/appsettings.json` — added `ModelVersion: "1.0.0"`
  - NuGet: Swashbuckle.AspNetCore 10.1.7, Serilog.AspNetCore 10.0.0, Serilog.Sinks.File 7.0.0, Serilog.Sinks.Console 6.1.1, FluentValidation.AspNetCore 11.3.1, Microsoft.ML 5.0.0
  - `dotnet build` → 0 errors, 0 warnings ✅
  - Swagger `/swagger` — all 6 endpoints visible ✅
  - **Note:** `OpenApiInfo` lives in `Microsoft.OpenApi` namespace in v2.x (not `Microsoft.OpenApi.Models`)

- [x] Phase 4 — Docker + docker-compose orchestration (2026-03-31)
  - `Dockerfile` — multi-stage (sdk:8.0 build → aspnet:8.0 runtime); curl install for healthcheck; `mkdir -p /app/data /app/logs`
  - `docker-compose.yml` — volume mounts `./data:/app/data` + `./logs:/app/logs`; healthcheck `GET /health`; double-underscore env vars for ASP.NET Core config
  - `.dockerignore` — excludes bin/, obj/, logs/, data/*.json, data/*.zip, .git/, .vs/, node_modules/
  - `AgriPredict.Api/appsettings.Production.json` — Production overrides for DataPaths + ModelVersion
  - `docker compose up -d --build agripredict-api` → container running ✅
  - `curl http://localhost:5080/health` → `{"status":"healthy"}` ✅
  - Serilog startup logged; `[ModelService] model.zip not found` ERR expected at first start (no data yet) ✅
  - **Note:** `aspnet:8.0` (Debian Bookworm) ships without curl — must `apt-get install curl` in runtime stage for healthcheck

- [x] Phase 5 — GitHub Pages demo UI (Leaflet.js map) (2026-03-31)
  - `docs/index.html` — two-column layout (60% map / 40% panel); local API notice banner; coordinate display; Analyse button; results panel; Uppsala demo button
  - `docs/style.css` — "field instrument" dark agri theme; CSS variables; mobile responsive (<700px stacked)
  - `docs/app.js` — Leaflet map; dark tile filter; click-to-place marker; parallel frost+rainfall fetch; error card; Uppsala pre-pin auto-predict
  - `docs/.nojekyll` — prevents GitHub Pages Jekyll processing
  - Live demo URL: https://okalangkenneth.github.io/AgriPredict/
  - **Note:** API not publicly hosted — banner in UI explains docker-compose local setup

### 🔨 IN PROGRESS
- [ ] Phase 6 — README + architecture diagram + LinkedIn post

---

## Phase Plan

### Phase 1 — Project Scaffold + Data Ingestion
**Goal:** Working .NET 8 solution that fetches and persists historical weather data.

**Deliverables:**
- `AgriPredict.sln`
- `AgriPredict.Api/` — .NET 8 minimal API (entry point)
- `AgriPredict.DataIngestion/` — Open-Meteo client, JSON persistence
- `AgriPredict.Core/` — Domain models (`WeatherObservation`, `FrostRiskLabel`)
- Historical data for Uppsala, SE (lat 59.86, lon 17.64) saved to `data/weather-history.json`

**Open-Meteo endpoint:**
```
https://archive-api.open-meteo.com/v1/archive
  ?latitude=59.86&longitude=17.64
  &start_date=2020-01-01&end_date=2024-12-31
  &daily=temperature_2m_min,temperature_2m_max,precipitation_sum,windspeed_10m_max
  &timezone=Europe/Stockholm
```

**Libraries:** `System.Net.Http.Json`, optionally `CsvHelper`

---

### Phase 2 — ML.NET Model Training
**Goal:** Train a binary classification model predicting frost risk (min temp ≤ 0°C in next 48h).

**Feature vector (per day):**
| Feature | Source |
|---------|--------|
| `TempMin` | temperature_2m_min |
| `TempMax` | temperature_2m_max |
| `Precipitation` | precipitation_sum |
| `WindSpeed` | windspeed_10m_max |
| `DayOfYear` | Derived — seasonality proxy |

**Label:** `FrostRisk` = 1 if `TempMin` ≤ 0.0 within next 2 days, else 0

**Algorithm:** `FastForest` binary classifier — strong baseline, interpretable feature importance  
**Train/test split:** 80/20, fixed `seed: 42` for reproducibility

**Deliverables:**
- `AgriPredict.Training/` — training pipeline + model serialisation
- `data/model.zip` — trained ML.NET model artifact
- Console output: Accuracy, AUC, F1 on test split

**Libraries:** `Microsoft.ML`, `Microsoft.ML.FastTree`

---

### Phase 3 — Prediction REST API
**Goal:** Expose frost risk and rainfall probability predictions via clean REST endpoints.

**Endpoints:**
```
GET  /health
GET  /api/v1/predict/frost?lat={lat}&lon={lon}
GET  /api/v1/predict/rainfall?lat={lat}&lon={lon}&days={1..7}
GET  /api/v1/weather/current?lat={lat}&lon={lon}
```

**Frost response shape:**
```json
{
  "location": { "lat": 59.86, "lon": 17.64 },
  "frostRiskProbability": 0.73,
  "frostRiskLabel": "High",
  "forecastWindowHours": 48,
  "modelVersion": "1.0.0",
  "generatedAt": "2026-03-31T10:00:00Z"
}
```

**Deliverables:**
- Swagger/OpenAPI on all endpoints (`/swagger`)
- `ProblemDetails` error responses (RFC 7807)
- Input validation — lat/lon range, days 1–7
- Serilog structured logging to console + file sink

**Libraries:** `Swashbuckle.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.File`, `FluentValidation.AspNetCore`

---

### Phase 4 — Docker + docker-compose
**Goal:** Fully containerised, single-command startup.

**Services:**
| Service | Image | Port |
|---------|-------|------|
| `agripredict-api` | Custom Dockerfile (.NET 8) | 5080 |

**Deliverables:**
- `Dockerfile` — multi-stage: SDK build → `aspnet:8.0` runtime
- `docker-compose.yml`
- Volume mount: `./data:/app/data` (model + training data persist across restarts)
- Health check: `GET /health` returns HTTP 200

**Rule:** Always `docker-compose up -d --build agripredict-api` — never just `restart`.

---

### Phase 5 — GitHub Pages Demo UI
**Goal:** Visual demo for portfolio visitors — click a map location, see frost risk prediction.

**Tech:** Plain HTML + Leaflet.js + `fetch()` to the API

**Features:**
- World map (Leaflet, OpenStreetMap tiles)
- Click anywhere → lat/lon sent to `/api/v1/predict/frost`
- Result panel: Frost Risk %, colour-coded label (🟢 Low / 🟡 Medium / 🔴 High)
- Pre-pinned demo location: Uppsala, SE

**Deliverables:** `docs/index.html`, `docs/app.js`, `docs/style.css`

---

### Phase 6 — README + Architecture Diagram + LinkedIn Post
**Goal:** Professional-grade portfolio presentation.

**README sections:**
1. Project overview + agriculture motivation
2. Architecture diagram (Mermaid)
3. Tech stack table
4. Quick start (`docker-compose up -d`)
5. API reference (endpoint table)
6. Model accuracy metrics (from Phase 2 output)
7. "What I'd add for production" section

**"What I'd add for production":**
| Addition | Problem it solves |
|----------|-------------------|
| Automated model retraining pipeline | Model drift as climate patterns shift year-on-year |
| OpenTelemetry + Grafana | Distributed traces, prediction latency dashboards |
| PostGIS + spatial queries | Multi-field farm boundary predictions, not just point coords |
| Feature store (e.g. Feast) | Consistent feature engineering between training and inference |
| JWT authentication | Multi-tenant farm operator access control |
| Kafka event stream | Real-time sensor data ingestion from IoT field devices |
| CI/CD (GitHub Actions) | Automated test + Docker build + deploy on every push |
| SHAP value explainability | Farmers must understand and trust what drives a prediction |

---

## Tech Stack

| Concern | Technology |
|---------|-----------|
| Framework | .NET 8 Minimal API |
| ML | ML.NET 3.x (`Microsoft.ML`, `Microsoft.ML.FastTree`) |
| Weather Data | Open-Meteo Archive API (free, no API key required) |
| Logging | Serilog + Console + File sinks |
| Validation | FluentValidation |
| API Docs | Swashbuckle / Swagger UI |
| Containers | Docker + docker-compose |
| Demo UI | Leaflet.js (GitHub Pages, `docs/` folder) |
| Version Control | Git → GitHub (`main` branch) |

---

## Workflow

```
1. Make changes
2. Build:    dotnet build
3. Test:     dotnet test
4. Run:      dotnet run --project AgriPredict.Api
5. Verify:   http://localhost:5080/swagger
6. Commit:   conventional commits (feat:, fix:, chore:, docs:)
7. Push:     git push origin main
```

---

## Git Conventions

- **Branch:** `main` = production
- **Commits:** `feat:`, `fix:`, `chore:`, `docs:`, `refactor:`, `test:`
- **Before every commit:** `dotnet build && dotnet test`
- **Never commit:** `appsettings.Development.json` with real values, API keys, `data/model.zip` (add to `.gitignore`)

### First-time setup (run once at start of Phase 1)
```bash
git init
git add .
git commit -m "feat: initial project scaffold"
git remote add origin https://github.com/okalangkenneth/AgriPredict.git
git branch -M main
git push -u origin main
```

### After every completed phase
```bash
git add .
git commit -m "feat: Phase X — <short description>"
git push origin main
```

---

## Critical Rules

### No Placeholder Code
- NO `TODO`, `FIXME`, `YOUR_API_KEY`, or magic hardcoded values outside tests
- All config in `appsettings.json` or environment variables

### ML.NET Rules
- Serialise trained model to `data/model.zip` — **never** retrain on every API request
- Load model once at startup via `IModelService` singleton
- Always log Accuracy, AUC, F1 during training — never ship without seeing metrics
- Fixed random seed `seed: 42` for reproducibility

### Docker Rules
- `docker-compose up -d --build agripredict-api` to pick up code changes (never just `restart`)
- Mount `./data:/app/data` so model survives container restarts
- `/health` endpoint must return HTTP 200 before marking a phase done

### Code Quality
- Remove unused `using` statements
- `async/await` throughout — no `.Result` or `.Wait()`
- `ILogger<T>` injected everywhere — no `Console.WriteLine` in production code
- `ProblemDetails` for all error responses (RFC 7807)

### Before Every Change
- Only modify what was explicitly requested
- If < 90% confident on approach, ask before coding
- Offer 2–3 options for significant architecture decisions

---

## Corrections Log

| Date | Mistake | Rule Added |
|------|---------|------------|
| | | |

---

## Session Management

### Starting a Session
1. CLAUDE.md auto-loads — trust the Build Progress section
2. Run `dotnet restore` if `obj/` folders are missing
3. Read any file before editing it

### End-of-Session Prompt
```
Update the Build Progress section in CLAUDE.md marking completed items,
then commit and push: git add . && git commit -m "chore: update CLAUDE.md" && git push origin main
```
